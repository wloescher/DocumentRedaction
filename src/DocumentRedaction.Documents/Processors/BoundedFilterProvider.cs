using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Util;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// Decorates PdfPig's filter provider so that no single stream may decode past
/// <see cref="MaxDecodedBytes"/> and the whole document stays under <see cref="MaxTotalDecodedBytes"/>.
/// Each decoder PdfPig ships is bounded before it runs, using what the format guarantees:
/// deflate cannot expand more than 1032:1, so a Flate stream is either small enough to be safe
/// or is trial-inflated (exactly as PdfPig inflates: two header bytes skipped, raw deflate) into a
/// scratch buffer to count its bytes; PdfPig's LZW never caps its table and reaches ~2559:1, and
/// RunLength 64:1, so those are held to their ratio because they cannot be measured without
/// allocating; CCITT output and the PNG/TIFF predictor rows are sized from the stream dictionary
/// and are checked from it. Everything else is checked after decoding, where the format gives
/// no expansion at all (ASCII filters) or PdfPig does not decode (DCT, JPX, JBIG2).
/// One instance serves one document: the first rejection is remembered and every later decode
/// fails fast, because PdfPig swallows filter errors on some paths (xref streams, fonts).
/// </summary>
internal sealed class BoundedFilterProvider : IFilterProvider
{
    private const long FlateMaxRatio = 1032;
    private const long LzwMaxRatio = 2560;
    private const long RunLengthMaxRatio = 64;
    private const int FlateHeaderBytes = 2;
    private const int ScratchSize = 64 * 1024;

    // PdfPig's defaults for the predictor and CCITT parameters, so the bound matches its decode.
    private const int DefaultColors = 1;
    private const int MaxColors = 32;
    private const int DefaultBitsPerComponent = 8;
    private const int DefaultColumns = 1;
    private const int DefaultCcittColumns = 1728;

    private readonly IFilterProvider _inner;
    private readonly Dictionary<IFilter, IFilter> _wrappers = new(ReferenceEqualityComparer.Instance);

    public BoundedFilterProvider(long maxDecodedBytes, long maxTotalDecodedBytes, IFilterProvider? inner = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDecodedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalDecodedBytes, 1);
        MaxDecodedBytes = maxDecodedBytes;
        MaxTotalDecodedBytes = maxTotalDecodedBytes;
        _inner = inner ?? DefaultFilterProvider.Instance;
    }

    public long MaxDecodedBytes { get; }

    public long MaxTotalDecodedBytes { get; }

    /// <summary>Bytes produced so far by decoding, plus bytes counted while measuring Flate streams.</summary>
    public long TotalDecodedBytes { get; private set; }

    /// <summary>The first cap violation seen while decoding, or null when every stream fit.</summary>
    public DocumentLimitExceededException? Rejection { get; private set; }

    public IReadOnlyList<IFilter> GetFilters(DictionaryToken dictionary) => Wrap(_inner.GetFilters(dictionary));

    public IReadOnlyList<IFilter> GetNamedFilters(IReadOnlyList<NameToken> names) => Wrap(_inner.GetNamedFilters(names));

    public IReadOnlyList<IFilter> GetAllFilters() => Wrap(_inner.GetAllFilters());

    /// <summary>Rethrows the remembered rejection, for callers that reach a point PdfPig should not have reached.</summary>
    public void ThrowIfLimitExceeded()
    {
        if (Rejection is { } rejection)
        {
            throw new DocumentLimitExceededException(rejection.Message, rejection);
        }
    }

    private IFilter[] Wrap(IReadOnlyList<IFilter> filters)
    {
        if (filters.Count == 0)
        {
            return [];
        }

        IFilter[] wrapped = new IFilter[filters.Count];
        for (int i = 0; i < filters.Count; i++)
        {
            if (!_wrappers.TryGetValue(filters[i], out IFilter? wrapper))
            {
                wrapper = new BoundedFilter(filters[i], this);
                _wrappers[filters[i]] = wrapper;
            }

            wrapped[i] = wrapper;
        }

        return wrapped;
    }

    private Memory<byte> Decode(IFilter filter, Memory<byte> input, DictionaryToken streamDictionary, IFilterProvider filterProvider, int filterIndex)
    {
        ThrowIfLimitExceeded();

        DictionaryToken parameters = DecodeParameterResolver.GetFilterParameters(streamDictionary, filterIndex);
        switch (filter)
        {
            case FlateFilter:
                RejectIfPredictorRowTooLong(parameters);
                RejectIfInflatesPastLimit(input);
                break;
            case LzwFilter:
                RejectIfPredictorRowTooLong(parameters);
                RejectIfRatioCouldExceed(input.Length, LzwMaxRatio);
                break;
            case RunLengthFilter:
                RejectIfRatioCouldExceed(input.Length, RunLengthMaxRatio);
                break;
            case CcittFaxDecodeFilter:
                RejectIfCcittOutputTooLarge(parameters, streamDictionary);
                break;
            default:
                break;
        }

        Memory<byte> output = filter.Decode(input, streamDictionary, filterProvider, filterIndex);
        if (output.Length > MaxDecodedBytes)
        {
            throw RejectStream();
        }

        Account(output.Length);
        return output;
    }

    /// <summary>Flate and LZW pipe their output through a predictor that allocates two rows sized from DecodeParms.</summary>
    private void RejectIfPredictorRowTooLong(DictionaryToken parameters)
    {
        if (parameters.GetIntOrDefault(NameToken.Predictor, -1) <= 1)
        {
            return;
        }

        long colors = Math.Min(parameters.GetIntOrDefault(NameToken.Colors, DefaultColors), MaxColors);
        long bitsPerComponent = parameters.GetIntOrDefault(NameToken.BitsPerComponent, DefaultBitsPerComponent);
        long columns = parameters.GetIntOrDefault(NameToken.Columns, DefaultColumns);
        long rowLength = (columns * colors * bitsPerComponent + 7) / 8;
        if (rowLength > MaxDecodedBytes)
        {
            throw RejectStream();
        }
    }

    /// <summary>CCITT allocates its whole output from the dictionary before reading any input.</summary>
    private void RejectIfCcittOutputTooLarge(DictionaryToken parameters, DictionaryToken streamDictionary)
    {
        long columns = parameters.GetIntOrDefault(NameToken.Columns, DefaultCcittColumns);
        long rows = Math.Max(
            parameters.GetIntOrDefault(NameToken.Rows, 0),
            streamDictionary.GetIntOrDefault(NameToken.Height, NameToken.H, 0));
        if ((columns + 7) / 8 * rows > MaxDecodedBytes)
        {
            throw RejectStream();
        }
    }

    private void RejectIfRatioCouldExceed(long inputLength, long maxRatio)
    {
        if (inputLength * maxRatio > MaxDecodedBytes)
        {
            throw RejectStreamByRatio();
        }
    }

    private void RejectIfInflatesPastLimit(ReadOnlyMemory<byte> input)
    {
        if (input.Length * FlateMaxRatio <= MaxDecodedBytes)
        {
            return;
        }

        long? inflated = MeasureInflated(input);
        if (inflated is null)
        {
            // PdfPig cannot inflate it either and hands back the input unchanged; that size is checked after decoding.
            return;
        }

        if (inflated.Value > MaxDecodedBytes)
        {
            throw RejectStream();
        }

        Account(inflated.Value);
    }

    /// <summary>
    /// Counts inflated bytes without keeping them, stopping as soon as the cap is passed. Mirrors
    /// PdfPig: skip the two zlib header bytes and read raw deflate. Null means PdfPig would fail too.
    /// </summary>
    private long? MeasureInflated(ReadOnlyMemory<byte> input)
    {
        if (input.Length <= FlateHeaderBytes)
        {
            return null;
        }

        ReadOnlyMemory<byte> body = input[FlateHeaderBytes..];
        if (!MemoryMarshal.TryGetArray(body, out ArraySegment<byte> segment))
        {
            segment = new ArraySegment<byte>(body.ToArray());
        }

        using MemoryStream source = new(segment.Array!, segment.Offset, segment.Count, writable: false);
        using DeflateStream inflater = new(source, CompressionMode.Decompress);
        byte[] scratch = ArrayPool<byte>.Shared.Rent(ScratchSize);
        try
        {
            long total = 0;
            int read;
            while ((read = inflater.Read(scratch, 0, scratch.Length)) > 0)
            {
                total += read;
                if (total > MaxDecodedBytes)
                {
                    return total;
                }
            }

            return total;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private void Account(long bytes)
    {
        TotalDecodedBytes += bytes;
        if (TotalDecodedBytes > MaxTotalDecodedBytes)
        {
            throw Remember(new DocumentLimitExceededException(
                $"The PDF exceeds the total decoded-size limit of {MaxTotalDecodedBytes:N0} bytes."));
        }
    }

    private DocumentLimitExceededException RejectStream() =>
        Remember(new DocumentLimitExceededException(
            $"A stream in the PDF exceeds the decoded-size limit of {MaxDecodedBytes:N0} bytes."));

    private DocumentLimitExceededException RejectStreamByRatio() =>
        Remember(new DocumentLimitExceededException(
            $"A compressed stream in the PDF could exceed the decoded-size limit of {MaxDecodedBytes:N0} bytes."));

    private DocumentLimitExceededException Remember(DocumentLimitExceededException rejection)
    {
        Rejection ??= rejection;
        return rejection;
    }

    private sealed class BoundedFilter : IFilter
    {
        private readonly IFilter _inner;
        private readonly BoundedFilterProvider _owner;

        public BoundedFilter(IFilter inner, BoundedFilterProvider owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public bool IsSupported => _inner.IsSupported;

        public Memory<byte> Decode(Memory<byte> input, DictionaryToken streamDictionary, IFilterProvider filterProvider, int filterIndex) =>
            _owner.Decode(_inner, input, streamDictionary, filterProvider, filterIndex);
    }
}

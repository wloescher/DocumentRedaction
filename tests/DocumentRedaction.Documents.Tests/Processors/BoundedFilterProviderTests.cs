using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Tests.Fixtures;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Tokens;

namespace DocumentRedaction.Documents.Tests.Processors;

public class BoundedFilterProviderTests
{
    private const long Cap = 1_000_000;

    private static readonly DictionaryToken EmptyDictionary = new(new Dictionary<NameToken, IToken>());

    private static BoundedFilterProvider Provider(long cap = Cap, long total = long.MaxValue) => new(cap, total);

    private static IFilter Named(IFilterProvider provider, NameToken name) => Assert.Single(provider.GetNamedFilters([name]));

    private static Memory<byte> Decode(BoundedFilterProvider provider, NameToken filter, byte[] input, DictionaryToken? dictionary = null) =>
        Named(provider, filter).Decode(input, dictionary ?? EmptyDictionary, provider, 0);

    private static DictionaryToken Dictionary(params (NameToken Key, IToken Value)[] entries) =>
        new(entries.ToDictionary(e => e.Key, e => e.Value));

    /// <summary>A stream dictionary whose single filter carries the given DecodeParms.</summary>
    private static DictionaryToken WithDecodeParms(NameToken filter, params (NameToken Key, IToken Value)[] parms) =>
        Dictionary((NameToken.Filter, filter), (NameToken.DecodeParms, Dictionary(parms)));

    [Fact]
    public void Flate_within_limit_decodes_exactly_as_the_inner_filter()
    {
        byte[] compressed = PdfFixture.Deflate(10_000, zlib: true);
        BoundedFilterProvider provider = Provider(cap: 10_000);

        Memory<byte> bounded = Decode(provider, NameToken.FlateDecode, compressed);
        Memory<byte> plain = Named(DefaultFilterProvider.Instance, NameToken.FlateDecode).Decode(compressed, EmptyDictionary, DefaultFilterProvider.Instance, 0);

        Assert.Equal(plain.ToArray(), bounded.ToArray());
        Assert.Null(provider.Rejection);
    }

    [Fact]
    public void Zlib_framed_flate_bomb_is_rejected_before_decoding()
    {
        BoundedFilterProvider provider = Provider();
        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(
            () => Decode(provider, NameToken.FlateDecode, PdfFixture.Deflate((int)Cap + 1, zlib: true)));

        Assert.Contains("1,000,000", ex.Message, StringComparison.Ordinal);
        Assert.Same(ex, provider.Rejection);
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Two_junk_header_bytes_before_raw_deflate_are_measured_the_way_pdfpig_reads_them()
    {
        // PdfPig never validates the zlib header: it skips two bytes and raw-inflates whatever follows.
        byte[] input = [0x00, 0x00, .. PdfFixture.Deflate((int)Cap + 1, zlib: false)];
        BoundedFilterProvider provider = Provider();

        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.FlateDecode, input));
    }

    [Fact]
    public void Raw_deflate_without_header_is_not_a_bomb_because_pdfpig_cannot_read_it_either()
    {
        byte[] input = PdfFixture.Deflate((int)Cap + 1, zlib: false);
        BoundedFilterProvider provider = Provider();

        Memory<byte> output = Decode(provider, NameToken.FlateDecode, input);

        Assert.Equal(input, output.ToArray());
        Assert.Null(provider.Rejection);
    }

    [Fact]
    public void Small_flate_stream_is_not_measured_at_all()
    {
        // 500 bytes cannot inflate past 516,000, so only the decoded output is accounted.
        byte[] compressed = PdfFixture.Deflate(200_000, zlib: true);
        Assert.True(compressed.Length * 1032 <= Cap);
        BoundedFilterProvider provider = Provider();

        Memory<byte> output = Decode(provider, NameToken.FlateDecode, compressed);

        Assert.Equal(200_000, output.Length);
        Assert.Equal(200_000, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Large_flate_stream_is_measured_and_both_passes_are_accounted()
    {
        // Incompressible data stays large on the wire, so the 1032:1 bound cannot clear it and it is measured.
        byte[] random = new byte[500_000];
        new Random(42).NextBytes(random);
        byte[] compressed = Zlib(random);
        Assert.True((long)compressed.Length * 1032 > Cap);
        BoundedFilterProvider provider = Provider();

        Memory<byte> output = Decode(provider, NameToken.FlateDecode, compressed);

        Assert.Equal(random, output.ToArray());
        Assert.Equal(1_000_000, provider.TotalDecodedBytes);
    }

    private static byte[] Zlib(byte[] data)
    {
        using MemoryStream buffer = new();
        using (System.IO.Compression.ZLibStream deflater = new(buffer, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            deflater.Write(data);
        }

        return buffer.ToArray();
    }

    [Fact]
    public void Predictor_with_huge_row_is_rejected_before_decoding()
    {
        DictionaryToken dictionary = WithDecodeParms(NameToken.FlateDecode, (NameToken.Predictor, new NumericToken(2)), (NameToken.Columns, new NumericToken(100_000_000)));
        BoundedFilterProvider provider = Provider();

        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.FlateDecode, PdfFixture.Deflate(10, zlib: true), dictionary));
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Predictor_with_ordinary_row_passes()
    {
        DictionaryToken dictionary = WithDecodeParms(NameToken.FlateDecode, (NameToken.Predictor, new NumericToken(12)), (NameToken.Columns, new NumericToken(4)));
        BoundedFilterProvider provider = Provider();

        Decode(provider, NameToken.FlateDecode, PdfFixture.Deflate(10, zlib: true), dictionary);

        Assert.Null(provider.Rejection);
    }

    [Fact]
    public void Lzw_input_that_could_exceed_the_cap_is_rejected_without_decoding()
    {
        byte[] input = new byte[(int)(Cap / 2560) + 1];
        BoundedFilterProvider provider = Provider();

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.LzwDecode, input));

        Assert.Contains("could exceed", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Small_lzw_input_reaches_the_inner_filter()
    {
        byte[] input = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];
        BoundedFilterProvider provider = Provider();

        Exception? ex = Record.Exception(() => Decode(provider, NameToken.LzwDecode, input));

        Assert.IsNotType<DocumentLimitExceededException>(ex);
        Assert.Null(provider.Rejection);
    }

    [Fact]
    public void Run_length_input_that_could_exceed_the_cap_is_rejected_without_decoding()
    {
        byte[] input = new byte[(int)(Cap / 64) + 1];
        BoundedFilterProvider provider = Provider();

        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.RunLengthDecode, input));
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Run_length_within_ratio_decodes_and_is_checked_afterwards()
    {
        // 0x81 followed by a byte repeats it 128 times; 100 pairs make 12,800 bytes.
        byte[] input = [.. Enumerable.Repeat(new byte[] { 0x81, (byte)'x' }, 100).SelectMany(pair => pair), 0x80];
        Memory<byte> output = Decode(Provider(), NameToken.RunLengthDecode, input);
        Assert.Equal(12_800, output.Length);

        BoundedFilterProvider tight = Provider(cap: 12_799);
        Assert.Throws<DocumentLimitExceededException>(() => Decode(tight, NameToken.RunLengthDecode, input));
    }

    [Fact]
    public void Ccitt_output_sized_from_the_dictionary_is_rejected_before_decoding()
    {
        DictionaryToken dictionary = WithDecodeParms(NameToken.CcittfaxDecode, (NameToken.Columns, new NumericToken(100_000)), (NameToken.Rows, new NumericToken(100_000)));
        BoundedFilterProvider provider = Provider();

        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.CcittfaxDecode, [0x00], dictionary));
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Ccitt_height_on_the_stream_dictionary_counts_as_rows()
    {
        DictionaryToken dictionary = Dictionary(
            (NameToken.Filter, NameToken.CcittfaxDecode),
            (NameToken.Height, new NumericToken(100_000)),
            (NameToken.DecodeParms, Dictionary((NameToken.Columns, new NumericToken(100_000)))));

        Assert.Throws<DocumentLimitExceededException>(() => Decode(Provider(), NameToken.CcittfaxDecode, [0x00], dictionary));
    }

    [Fact]
    public void Non_expanding_filter_output_over_limit_is_rejected_after_decoding()
    {
        BoundedFilterProvider provider = Provider(cap: 2);

        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray()));
        Assert.NotNull(provider.Rejection);
    }

    [Fact]
    public void Non_expanding_filter_output_within_limit_passes()
    {
        BoundedFilterProvider provider = Provider(cap: 3);

        Memory<byte> output = Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray());

        Assert.Equal("ABC"u8.ToArray(), output.ToArray());
        Assert.Equal(3, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Total_budget_rejects_the_stream_that_crosses_it()
    {
        BoundedFilterProvider provider = Provider(cap: 10, total: 5);
        Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray());

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray()));

        Assert.Contains("total decoded-size limit of 5", ex.Message, StringComparison.Ordinal);
        Assert.Same(ex, provider.Rejection);
    }

    [Fact]
    public void After_a_rejection_every_decode_fails_fast()
    {
        BoundedFilterProvider provider = Provider(cap: 2);
        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray()));

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.AsciiHexDecode, "41>"u8.ToArray()));

        Assert.Same(provider.Rejection, ex.InnerException);
        Assert.Equal(0, provider.TotalDecodedBytes);
    }

    [Fact]
    public void Throw_if_limit_exceeded_is_quiet_until_a_rejection_then_wraps_the_first_one()
    {
        BoundedFilterProvider provider = Provider(cap: 2);
        provider.ThrowIfLimitExceeded();
        Assert.Throws<DocumentLimitExceededException>(() => Decode(provider, NameToken.AsciiHexDecode, "414243>"u8.ToArray()));

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(provider.ThrowIfLimitExceeded);

        Assert.Same(provider.Rejection, ex.InnerException);
        Assert.Equal(provider.Rejection!.Message, ex.Message);
    }

    [Fact]
    public void Undecodable_flate_data_is_left_to_the_inner_filter()
    {
        // Large enough to be measured, unreadable to both us and PdfPig, which hands it back as-is.
        byte[] garbage = new byte[2_000];
        Array.Fill(garbage, (byte)0xFF);
        BoundedFilterProvider provider = Provider();

        Memory<byte> output = Decode(provider, NameToken.FlateDecode, garbage);

        Assert.Equal(garbage, output.ToArray());
        Assert.Null(provider.Rejection);
    }

    [Fact]
    public void Wrappers_are_reused_across_lookups_and_cover_every_filter()
    {
        BoundedFilterProvider provider = Provider();
        Assert.Same(Named(provider, NameToken.FlateDecode), Named(provider, NameToken.FlateDecode));
        Assert.Same(Named(provider, NameToken.FlateDecode), Assert.Single(provider.GetFilters(Dictionary((NameToken.Filter, NameToken.FlateDecode)))));
        Assert.Equal(DefaultFilterProvider.Instance.GetAllFilters().Count, provider.GetAllFilters().Count);
        Assert.Empty(provider.GetFilters(EmptyDictionary));
    }

    [Fact]
    public void Supported_flag_is_forwarded()
    {
        IReadOnlyList<IFilter> inner = DefaultFilterProvider.Instance.GetAllFilters();
        IReadOnlyList<IFilter> wrapped = Provider().GetAllFilters();
        Assert.Equal(inner.Select(f => f.IsSupported), wrapped.Select(f => f.IsSupported));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Limits_below_one_are_rejected(long cap, long total) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedFilterProvider(cap, total));
}

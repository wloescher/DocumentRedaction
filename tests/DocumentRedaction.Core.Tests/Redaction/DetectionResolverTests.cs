using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Core.Tests.Redaction;

public class DetectionResolverTests
{
    private static Detection D(InformationKind kind, int start, int length) => new(kind, start, length, new string('x', length));

    [Fact]
    public void Empty_input_gives_empty_output() => Assert.Empty(DetectionResolver.Resolve([]));

    [Fact]
    public void Non_overlapping_are_kept_and_sorted_by_start()
    {
        var a = D(InformationKind.EmailAddress, 10, 5);
        var b = D(InformationKind.PhoneNumber, 0, 5);
        Assert.Equal([b, a], DetectionResolver.Resolve([a, b]));
    }

    [Fact]
    public void Adjacent_spans_do_not_overlap()
    {
        var a = D(InformationKind.EmailAddress, 0, 5);
        var b = D(InformationKind.PhoneNumber, 5, 5);
        Assert.Equal([a, b], DetectionResolver.Resolve([b, a]));
    }

    [Fact]
    public void Longer_span_wins_even_when_it_starts_later()
    {
        var shortFirst = D(InformationKind.CreditCardNumber, 0, 6);
        var longLater = D(InformationKind.PhoneNumber, 3, 10);
        Assert.Equal([longLater], DetectionResolver.Resolve([shortFirst, longLater]));
    }

    [Fact]
    public void Equal_length_resolves_by_kind_priority()
    {
        var phone = D(InformationKind.PhoneNumber, 0, 9);
        var routing = D(InformationKind.BankRoutingNumber, 0, 9);
        Assert.Equal([routing], DetectionResolver.Resolve([phone, routing]));
    }

    [Fact]
    public void Full_tie_prefers_earlier_start()
    {
        var later = D(InformationKind.Date, 2, 4);
        var earlier = D(InformationKind.Date, 0, 4);
        Assert.Equal([earlier], DetectionResolver.Resolve([later, earlier]));
    }

    [Fact]
    public void Contained_span_is_dropped()
    {
        var outer = D(InformationKind.ConfidentialStatement, 0, 40);
        var inner = D(InformationKind.EmailAddress, 10, 12);
        Assert.Equal([outer], DetectionResolver.Resolve([inner, outer]));
    }

    [Fact]
    public void Chain_of_overlaps_keeps_every_non_conflicting_span()
    {
        var a = D(InformationKind.Date, 0, 4);
        var b = D(InformationKind.Date, 3, 6); // overlaps a and c, longest
        var c = D(InformationKind.Date, 9, 4);
        var d = D(InformationKind.Date, 20, 3);
        Assert.Equal([b, c, d], DetectionResolver.Resolve([a, b, c, d]));
    }
}

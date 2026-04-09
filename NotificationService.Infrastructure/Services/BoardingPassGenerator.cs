using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NotificationService.Infrastructure.Services;

public static class BoardingPassGenerator
{
    private const string Navy = "#1a237e";
    private const string Gold = "#f9a825";
    private const string LightBg = "#f4f6fb";

    public static byte[] Generate(
        string passengerName,
        string flightNumber,
        string origin,
        string destination,
        DateTime departureTime,
        string seatNumber,
        string seatClass,
        string pnr,
        decimal amount)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(0);
                page.PageColor(Colors.White);   // ← replaces obsolete Background(Color)

                page.Content().Column(col =>
                {
                    // ─── HEADER ──────────────────────────────────────────────
                    col.Item().Background(Navy).Padding(16).Row(header =>
                    {
                        header.RelativeItem().Column(left =>
                        {
                            left.Item().Text("✈ Veloskyra")
                                .FontSize(22).Bold().FontColor(Colors.White);
                            left.Item().Text("BOARDING PASS")
                                .FontSize(9).FontColor(Gold).Bold()
                                .LetterSpacing(3);
                        });

                        header.ConstantItem(160).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(flightNumber)
                                .FontSize(26).Bold().FontColor(Gold);
                            right.Item().AlignRight().Text(seatClass.ToUpper())
                                .FontSize(10).FontColor(Colors.White);
                        });
                    });

                    // ─── ROUTE ROW ────────────────────────────────────────────
                    col.Item().Background(LightBg).Padding(20).Row(route =>
                    {
                        route.RelativeItem().AlignCenter().Column(o =>
                        {
                            o.Item().AlignCenter().Text(origin)
                                .FontSize(40).Bold().FontColor(Navy);
                            o.Item().AlignCenter().Text("Origin")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        route.ConstantItem(60).AlignCenter().AlignMiddle()
                            .Text("→").FontSize(28).FontColor(Gold);

                        route.RelativeItem().AlignCenter().Column(d =>
                        {
                            d.Item().AlignCenter().Text(destination)
                                .FontSize(40).Bold().FontColor(Navy);
                            d.Item().AlignCenter().Text("Destination")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    // ─── DETAILS ──────────────────────────────────────────────
                    col.Item().Padding(16).Row(details =>
                    {
                        details.RelativeItem().Column(left =>
                        {
                            DetailField(left, "PASSENGER", passengerName.ToUpper());
                            DetailField(left, "DEPARTURE", departureTime.ToString("ddd, dd MMM yyyy"));
                            DetailField(left, "BOARDING TIME",
                                departureTime.AddMinutes(-45).ToString("HH:mm") + " hrs");
                        });

                        // Solid thin separator
                        details.ConstantItem(1).Background(Colors.Grey.Lighten2).ExtendVertical();

                        details.RelativeItem().PaddingLeft(16).Column(right =>
                        {
                            DetailField(right, "SEAT", seatNumber);
                            DetailField(right, "CLASS", seatClass.ToUpper());
                            DetailField(right, "GATE CLOSES",
                                departureTime.AddMinutes(-15).ToString("HH:mm") + " hrs");
                        });
                    });

                    // ─── TEAR LINE ────────────────────────────────────────────
                    col.Item().PaddingHorizontal(16).PaddingVertical(4)
                        .LineHorizontal(1.5f).LineColor(Colors.Grey.Medium);

                    // ─── PNR BAR ──────────────────────────────────────────────
                    col.Item().Background(Navy).Padding(14).Row(pnrRow =>
                    {
                        pnrRow.RelativeItem().Column(pnrLeft =>
                        {
                            pnrLeft.Item().Text("BOOKING REFERENCE  /  PNR")
                                .FontSize(8).FontColor(Colors.White).Bold().LetterSpacing(2);
                            pnrLeft.Item().Text(pnr)
                                .FontSize(30).Bold().FontColor(Gold)
                                .FontFamily("Courier New");
                        });

                        pnrRow.ConstantItem(140).AlignRight().Column(r =>
                        {
                            r.Item().AlignRight().Text("AMOUNT PAID")
                                .FontSize(8).FontColor(Colors.White).Bold().LetterSpacing(1);
                            r.Item().AlignRight().Text($"₹{amount:N2}")
                                .FontSize(18).Bold().FontColor(Gold);
                            r.Item().PaddingTop(4).AlignRight()
                                .Text("Have a pleasant journey!")
                                .FontSize(8).FontColor(Colors.White).Italic();
                        });
                    });

                    // ─── FOOTER ───────────────────────────────────────────────
                    col.Item().Background(Colors.Grey.Lighten4).Padding(7)
                        .AlignCenter()
                        .Text("Please arrive at least 2 hours before departure · Veloskyra Airlines")
                        .FontSize(8).FontColor(Colors.Grey.Medium).Italic();
                });
            });
        }).GeneratePdf();
    }

    private static void DetailField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(8).Column(c =>
        {
            c.Item().Text(label)
                .FontSize(7).FontColor(Colors.Grey.Medium).Bold().LetterSpacing(2);
            c.Item().Text(value)
                .FontSize(13).Bold().FontColor(Navy);
        });
    }
}

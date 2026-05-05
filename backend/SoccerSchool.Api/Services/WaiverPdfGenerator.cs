using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

public interface IWaiverPdfGenerator
{
    byte[] GenerateForPlayer(Registration registration, RegistrationPlayer registrationPlayer);
    byte[] GenerateForRegistration(Registration registration);
}

/// <summary>
/// Las Vegas Soccer School waiver — adapted from the El Nuevo Mundo Mundialito waiver,
/// rebranded for LVSS. One waiver per player; combined PDF generates one section per player.
/// </summary>
public class WaiverPdfGenerator : IWaiverPdfGenerator
{
    private const string Brand = "Las Vegas Soccer School";
    private const string Accent = "#0a7d3b";
    private const string SectionBg = "#e2e8f0";

    public byte[] GenerateForPlayer(Registration r, RegistrationPlayer rp) =>
        Document.Create(c => RenderPlayerPage(c, r, rp)).GeneratePdf();

    public byte[] GenerateForRegistration(Registration r) =>
        Document.Create(c =>
        {
            RenderSummaryPage(c, r);
            foreach (var rp in r.Players)
                RenderPlayerPage(c, r, rp);
        }).GeneratePdf();

    private static void RenderSummaryPage(IDocumentContainer container, Registration r)
    {
        var es = r.Language == Language.Spanish;
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(28);
            page.DefaultTextStyle(s => s.FontSize(9.5f).FontFamily(Fonts.Arial));

            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(es ? "RESUMEN DE INSCRIPCIÓN" : "REGISTRATION SUMMARY")
                    .FontSize(15).Bold();
                col.Item().AlignCenter().Text(Brand).FontSize(11).SemiBold().FontColor(Accent);
                col.Item().AlignCenter().Text($"{(es ? "Temporada" : "Season")}: {r.Season}").FontSize(10);
                col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Accent);
            });

            page.Content().PaddingVertical(8).Column(col =>
            {
                col.Spacing(8);

                col.Item().Element(e => SectionHeader(e, es ? "Padre / Madre / Tutor" : "Parent / Guardian"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(140);
                        c.RelativeColumn();
                    });
                    LabelCell(table, es ? "Nombre:" : "Name:");
                    ValueCell(table, $"{r.ParentFirstName} {r.ParentLastName}");
                    LabelCell(table, es ? "Dirección:" : "Address:");
                    ValueCell(table, FormatAddress(r));
                    LabelCell(table, es ? "Teléfono:" : "Phone:");
                    ValueCell(table, r.CellPhone);
                    LabelCell(table, "Email:");
                    ValueCell(table, r.Email);
                    LabelCell(table, es ? "Idioma:" : "Language:");
                    ValueCell(table, es ? "Español" : "English");
                    LabelCell(table, es ? "Enviado:" : "Submitted:");
                    ValueCell(table, r.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));
                    LabelCell(table, es ? "Exenciones firmadas:" : "Waivers signed:");
                    ValueCell(table, $"{r.Players.Count(rp => !string.IsNullOrEmpty(rp.SignatureDataUrl))} / {r.Players.Count}");
                });

                col.Item().PaddingTop(4).Element(e =>
                    SectionHeader(e, $"{(es ? "Jugadores" : "Players")} ({r.Players.Count})"));

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f); // Name
                        c.ConstantColumn(75);   // DOB
                        c.ConstantColumn(55);   // Grade
                        c.ConstantColumn(65);   // Uniform
                        c.ConstantColumn(55);   // Shoe
                        c.ConstantColumn(55);   // Signed?
                    });

                    HeaderCell(table, es ? "Nombre" : "Name");
                    HeaderCell(table, es ? "Nac." : "DOB");
                    HeaderCell(table, es ? "Grado" : "Grade");
                    HeaderCell(table, es ? "Uniforme" : "Uniform");
                    HeaderCell(table, es ? "Calzado" : "Shoe");
                    HeaderCell(table, es ? "Firmada" : "Signed");

                    foreach (var rp in r.Players)
                    {
                        var p = rp.Player!;
                        BodyCell(table, $"{p.FirstName} {p.LastName}", bold: true);
                        BodyCell(table, p.DateOfBirth.ToString("yyyy-MM-dd"));
                        BodyCell(table, rp.SchoolGrade);
                        BodyCell(table, rp.UniformSize);
                        BodyCell(table, rp.ShoeSize);
                        BodyCell(table, !string.IsNullOrEmpty(rp.SignatureDataUrl) ? "✓" : "—");
                    }
                });

                if (r.Players.Any(rp => !string.IsNullOrWhiteSpace(rp.HeardFrom)))
                {
                    col.Item().PaddingTop(4).Element(e =>
                        SectionHeader(e, es ? "¿Cómo se enteraron de nosotros?" : "How they heard about us"));
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(160);
                            c.RelativeColumn();
                        });
                        foreach (var rp in r.Players.Where(rp => !string.IsNullOrWhiteSpace(rp.HeardFrom)))
                        {
                            var p = rp.Player!;
                            LabelCell(table, $"{p.FirstName} {p.LastName}:");
                            ValueCell(table, rp.HeardFrom!);
                        }
                    });
                }

                col.Item().PaddingTop(8).Text(es
                    ? $"Las exenciones firmadas individuales siguen — una página por jugador."
                    : $"Individual signed waivers follow — one page per player.")
                    .Italic().FontColor(Colors.Grey.Darken1).FontSize(8.5f);
            });

            page.Footer().AlignCenter().Text(s =>
                s.Span(Brand).FontSize(7.5f).FontColor(Colors.Grey.Medium));
        });
    }

    private static string FormatAddress(Registration r) =>
        string.Join(", ", new[] { r.AddressLine1, r.AddressLine2, $"{r.City}, {r.State} {r.PostalCode}" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static void HeaderCell(QuestPDF.Fluent.TableDescriptor table, string text) =>
        table.Cell().Background("#f1f5f9").BorderBottom(0.5f).BorderColor("#cbd5e1")
            .Padding(2).Text(text).SemiBold().FontSize(8.5f);

    private static void BodyCell(QuestPDF.Fluent.TableDescriptor table, string text, bool bold = false) =>
        table.Cell().BorderBottom(0.25f).BorderColor("#e2e8f0").Padding(2)
            .Text(t => { var span = t.Span(text); if (bold) span.SemiBold(); });

    private static void RenderPlayerPage(IDocumentContainer container, Registration r, RegistrationPlayer rp)
    {
        var t = WaiverText.For(r.Language);
        var p = rp.Player!;
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(24);
            page.DefaultTextStyle(s => s.FontSize(8.5f).FontFamily(Fonts.Arial));

            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(t.Heading1).FontSize(13).Bold();
                col.Item().AlignCenter().Text(t.Heading2).FontSize(10).SemiBold().FontColor(Accent);
                col.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor(Accent);
            });

            page.Content().PaddingVertical(4).Column(col =>
            {
                col.Spacing(3);

                col.Item().Element(e => SectionHeader(e, t.SectionParticipant));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(150);
                        c.RelativeColumn();
                    });
                    LabelCell(table, t.LblParticipantName); ValueCell(table, rp.WaiverParticipantName ?? $"{p.FirstName} {p.LastName}");
                    LabelCell(table, t.LblDob); ValueCell(table, p.DateOfBirth.ToString("yyyy-MM-dd"));
                    LabelCell(table, t.LblTeam); ValueCell(table, string.IsNullOrWhiteSpace(rp.WaiverTeamName) ? "—" : rp.WaiverTeamName!);
                    LabelCell(table, t.LblParentGuardian); ValueCell(table, rp.WaiverParentGuardianName ?? $"{r.ParentFirstName} {r.ParentLastName}");
                    LabelCell(table, t.LblPhone); ValueCell(table, rp.WaiverPhone ?? r.CellPhone);
                    LabelCell(table, t.LblEmail); ValueCell(table, rp.WaiverEmail ?? r.Email);
                });

                col.Item().Element(e => SectionHeader(e, t.SectionRisk));
                col.Item().Text(t.RiskIntro);
                col.Item().Text(s =>
                {
                    s.Span("• " + string.Join("  • ", t.RiskBullets));
                });
                col.Item().Text(s =>
                {
                    s.Span(t.RiskAcceptPrefix);
                    s.Span(t.RiskAcceptBold).Bold();
                    s.Span(".");
                });

                col.Item().Element(e => SectionHeader(e, t.SectionWaiver));
                col.Item().Text(s =>
                {
                    s.Span(t.WaiverIntro + " ");
                    s.Span(t.WaiverPartiesBold).Bold();
                    s.Span(" " + t.WaiverFromAny + " " + string.Join(", ", t.WaiverBullets) + " — " + t.WaiverNegligence);
                });

                col.Item().Element(e => SectionHeader(e, t.SectionMedical));
                col.Item().Text(s =>
                {
                    s.Span(t.MedicalIntro + " ");
                    s.Span(string.Join("; ", t.MedicalBullets) + ". ");
                    s.Span(t.MedicalCloser);
                });

                col.Item().Element(e => SectionHeader(e, t.SectionMedia));
                col.Item().Text(s =>
                {
                    s.Span(t.MediaIntro + " ");
                    s.Span(t.MediaPartiesBold).Bold();
                    s.Span(" " + t.MediaUse + " " + string.Join(", ", t.MediaBullets) + " — " + t.MediaCloser);
                });

                col.Item().Element(e => SectionHeader(e, t.SectionRules));
                col.Item().Text(s =>
                {
                    s.Span(t.RulesIntro + " ");
                    s.Span(string.Join("; ", t.RulesBullets) + ".");
                });

                col.Item().PaddingTop(4).Element(e => SectionHeader(e, t.SectionSignature));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(140);
                        c.RelativeColumn();
                        c.ConstantColumn(70);
                        c.ConstantColumn(120);
                    });
                    LabelCell(table, t.LblParentGuardian);
                    ValueCell(table, rp.WaiverParentGuardianName ?? $"{r.ParentFirstName} {r.ParentLastName}");
                    LabelCell(table, t.LblDate);
                    ValueCell(table, (rp.SignedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm 'UTC'"));
                });
                col.Item().PaddingTop(2).Text(t.LblSignature).SemiBold();
                col.Item().Border(0.5f).BorderColor("#94a3b8").Background("#f8fafc")
                    .Height(50).Element(box =>
                    {
                        var sigBytes = TryDecodeDataUrl(rp.SignatureDataUrl);
                        if (sigBytes is not null)
                            box.Padding(2).AlignCenter().AlignMiddle().MaxHeight(46).Image(sigBytes);
                        else
                            box.AlignCenter().AlignMiddle().Text("—").FontColor(Colors.Grey.Medium);
                    });
            });

            page.Footer().AlignCenter().Text(s =>
            {
                s.Span($"{Brand}").FontSize(7.5f).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void SectionHeader(IContainer e, string text) =>
        e.Background(SectionBg).PaddingVertical(2).PaddingHorizontal(5).Text(text).Bold().FontSize(9f);

    private static void LabelCell(QuestPDF.Fluent.TableDescriptor table, string label) =>
        table.Cell().Padding(1).Text(label).SemiBold();

    private static void ValueCell(QuestPDF.Fluent.TableDescriptor table, string value) =>
        table.Cell().Padding(1).Text(value);


    private static byte[]? TryDecodeDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var idx = dataUrl.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(idx + 7)..]); }
        catch { return null; }

        // Verify it's a real image so QuestPDF doesn't crash on garbage payloads.
        // PNG: 89 50 4E 47 0D 0A 1A 0A   JPEG: FF D8 FF
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return bytes;
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return bytes;
        return null;
    }
}

internal sealed record WaiverText(
    string Heading1, string Heading2,
    string SectionParticipant, string SectionRisk, string SectionWaiver,
    string SectionMedical, string SectionMedia, string SectionRules, string SectionSignature,
    string LblParticipantName, string LblDob, string LblTeam,
    string LblParentGuardian, string LblPhone, string LblEmail,
    string LblSignature, string LblDate, string LblPage,
    string RiskIntro, string[] RiskBullets, string RiskAcceptPrefix, string RiskAcceptBold,
    string WaiverIntro, string WaiverPartiesBold, string WaiverFromAny, string[] WaiverBullets, string WaiverNegligence,
    string MedicalIntro, string[] MedicalBullets, string MedicalCloser,
    string MediaIntro, string MediaPartiesBold, string MediaUse, string[] MediaBullets, string MediaCloser,
    string RulesIntro, string[] RulesBullets)
{
    public static WaiverText For(Language lang) => lang == Language.Spanish ? Spanish : English;

    private static readonly WaiverText English = new(
        Heading1: "WAIVER & LIABILITY RELEASE FORM",
        Heading2: "Las Vegas Soccer School",
        SectionParticipant: "Participant Information",
        SectionRisk: "ASSUMPTION OF RISK",
        SectionWaiver: "WAIVER OF LIABILITY",
        SectionMedical: "MEDICAL AUTHORIZATION",
        SectionMedia: "MEDIA RELEASE",
        SectionRules: "RULES ACKNOWLEDGMENT",
        SectionSignature: "SIGNATURE",
        LblParticipantName: "Participant Name:",
        LblDob: "Date of Birth:",
        LblTeam: "Team Name:",
        LblParentGuardian: "Parent / Guardian Name:",
        LblPhone: "Phone Number:",
        LblEmail: "Email:",
        LblSignature: "Signature:",
        LblDate: "Date:",
        LblPage: "Page",
        RiskIntro: "I, the undersigned parent or legal guardian, understand that participation in Las Vegas Soccer School programs and activities involves inherent risks, including but not limited to:",
        RiskBullets: new[]
        {
            "Physical injury",
            "Collisions with other players",
            "Falls or accidents on the field",
            "Environmental conditions (weather, field conditions, etc.)"
        },
        RiskAcceptPrefix: "I voluntarily allow my child to participate and ",
        RiskAcceptBold: "assume all risks associated with this activity",
        WaiverIntro: "I hereby release, waive, and discharge:",
        WaiverPartiesBold: "Las Vegas Soccer School, its coaches, organizers, staff, volunteers, sponsors, and affiliates",
        WaiverFromAny: "from any and all liability, claims, demands, or causes of action arising out of or related to:",
        WaiverBullets: new[] { "Injury", "Illness", "Loss or damage of personal property" },
        WaiverNegligence: "whether caused by negligence or otherwise, to the fullest extent permitted by law.",
        MedicalIntro: "In the event of an emergency, I authorize program staff to:",
        MedicalBullets: new[]
        {
            "Seek medical treatment for my child",
            "Transport my child to a medical facility if necessary"
        },
        MedicalCloser: "I understand that I am responsible for any medical expenses incurred.",
        MediaIntro: "I grant permission to:",
        MediaPartiesBold: "Las Vegas Soccer School and its partners",
        MediaUse: "to use photographs, videos, or recordings of my child taken during programs and events for:",
        MediaBullets: new[] { "Promotional purposes", "Advertising", "Social media", "Media coverage" },
        MediaCloser: "without compensation.",
        RulesIntro: "I confirm that I have read and agree to the official program rules and understand that:",
        RulesBullets: new[]
        {
            "The organizers have full authority to enforce rules",
            "Decisions made during the program are final"
        }
    );

    private static readonly WaiverText Spanish = new(
        Heading1: "FORMULARIO DE EXENCIÓN Y LIBERACIÓN DE RESPONSABILIDAD",
        Heading2: "Las Vegas Soccer School",
        SectionParticipant: "Información del participante",
        SectionRisk: "ASUNCIÓN DE RIESGO",
        SectionWaiver: "EXENCIÓN DE RESPONSABILIDAD",
        SectionMedical: "AUTORIZACIÓN MÉDICA",
        SectionMedia: "AUTORIZACIÓN DE USO DE IMAGEN",
        SectionRules: "RECONOCIMIENTO DE REGLAS",
        SectionSignature: "FIRMA",
        LblParticipantName: "Nombre del participante:",
        LblDob: "Fecha de nacimiento:",
        LblTeam: "Nombre del equipo:",
        LblParentGuardian: "Nombre del padre/madre/tutor:",
        LblPhone: "Teléfono:",
        LblEmail: "Correo electrónico:",
        LblSignature: "Firma:",
        LblDate: "Fecha:",
        LblPage: "Página",
        RiskIntro: "Yo, el padre, madre o tutor legal abajo firmante, entiendo que la participación en los programas y actividades de Las Vegas Soccer School conlleva riesgos inherentes, incluyendo pero no limitado a:",
        RiskBullets: new[]
        {
            "Lesiones físicas",
            "Colisiones con otros jugadores",
            "Caídas o accidentes en el campo",
            "Condiciones del entorno (clima, condiciones del campo, etc.)"
        },
        RiskAcceptPrefix: "Permito voluntariamente que mi hijo(a) participe y ",
        RiskAcceptBold: "asumo todos los riesgos asociados con esta actividad",
        WaiverIntro: "Por la presente libero, renuncio y eximo a:",
        WaiverPartiesBold: "Las Vegas Soccer School, sus entrenadores, organizadores, personal, voluntarios, patrocinadores y afiliados",
        WaiverFromAny: "de toda responsabilidad, reclamación, demanda o causa de acción que surja de o esté relacionada con:",
        WaiverBullets: new[] { "Lesiones", "Enfermedad", "Pérdida o daño de bienes personales" },
        WaiverNegligence: "ya sea causada por negligencia o de otra manera, en la máxima medida permitida por la ley.",
        MedicalIntro: "En caso de emergencia, autorizo al personal del programa a:",
        MedicalBullets: new[]
        {
            "Buscar tratamiento médico para mi hijo(a)",
            "Transportar a mi hijo(a) a un centro médico si es necesario"
        },
        MedicalCloser: "Entiendo que soy responsable de cualquier gasto médico incurrido.",
        MediaIntro: "Concedo permiso a:",
        MediaPartiesBold: "Las Vegas Soccer School y sus socios",
        MediaUse: "para usar fotografías, videos o grabaciones de mi hijo(a) tomadas durante los programas y eventos con fines de:",
        MediaBullets: new[] { "Promoción", "Publicidad", "Redes sociales", "Cobertura mediática" },
        MediaCloser: "sin compensación.",
        RulesIntro: "Confirmo que he leído y acepto las reglas oficiales del programa y entiendo que:",
        RulesBullets: new[]
        {
            "Los organizadores tienen plena autoridad para hacer cumplir las reglas",
            "Las decisiones tomadas durante el programa son finales"
        }
    );
}

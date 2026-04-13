using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace NoteInfrastructure.Services.Docx
{
    /// <summary>
    /// Допоміжні методи для роботи з .docx через OpenXML SDK.
    /// </summary>
    internal static class DocxHelper
    {
        // ── Стилі ──────────────────────────────────────────────────────────

        /// <summary>Заголовок 1-го рівня (назва каталогу).</summary>
        public static Paragraph Heading1(string text)
            => StyledParagraph(text, "Heading1", bold: true, fontSize: 28);

        /// <summary>Заголовок 2-го рівня (назва файлу).</summary>
        public static Paragraph Heading2(string text)
            => StyledParagraph(text, "Heading2", bold: true, fontSize: 24);

        /// <summary>Звичайний абзац.</summary>
        public static Paragraph NormalParagraph(string text, bool bold = false)
        {
            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            if (bold)
                run.RunProperties = new RunProperties(new Bold());
            return new Paragraph(run);
        }

        /// <summary>Горизонтальний роздільник (порожній абзац із нижньою межею).</summary>
        public static Paragraph HorizontalRule()
        {
            var pBorders = new ParagraphBorders(
                new BottomBorder
                {
                    Val   = BorderValues.Single,
                    Size  = 6,
                    Space = 1,
                    Color = "AAAAAA",
                });
            var pPr = new ParagraphProperties();
            pPr.AppendChild(pBorders);
            return new Paragraph(pPr);
        }

        // ── Таблиці ────────────────────────────────────────────────────────

        /// <summary>Будує таблицю з двох колонок (мітка | значення) для деталей файлу.</summary>
        public static Table DetailsTable(IEnumerable<(string Label, string Value)> rows)
        {
            var table = new Table();

            // Властивості таблиці: межі
            table.AppendChild(new TableProperties(
                new TableBorders(
                    new TopBorder    { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                    new LeftBorder   { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                    new RightBorder  { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                    new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }
                ),
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" } // 100 %
            ));

            foreach (var (label, value) in rows)
            {
                var tr = new TableRow();
                tr.AppendChild(TableCell(label, bold: true, widthTwips: 2000));
                tr.AppendChild(TableCell(value ?? string.Empty, bold: false));
                table.AppendChild(tr);
            }

            return table;
        }

        // ── Приватні допоміжники ────────────────────────────────────────────

        private static Paragraph StyledParagraph(
            string text, string styleId, bool bold, int fontSize)
        {
            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            run.RunProperties = new RunProperties(
                new Bold { Val = bold ? OnOffValue.FromBoolean(true) : OnOffValue.FromBoolean(false) },
                new FontSize { Val = (fontSize * 2).ToString() }   // OpenXML: half-points
            );
            var pp = new ParagraphProperties(new ParagraphStyleId { Val = styleId });
            return new Paragraph(pp, run);
        }

        private static TableCell TableCell(string text, bool bold, int? widthTwips = null)
        {
            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            if (bold) run.RunProperties = new RunProperties(new Bold());

            var cellProps = new TableCellProperties();
            if (widthTwips.HasValue)
                cellProps.AppendChild(
                    new TableCellWidth { Type = TableWidthUnitValues.Dxa,
                                         Width = widthTwips.Value.ToString() });

            var cell = new TableCell();
            cell.AppendChild(cellProps);
            cell.AppendChild(new Paragraph(run));
            return cell;
        }

        // ── Читання ────────────────────────────────────────────────────────

        /// <summary>Повертає текст абзацу, об'єднуючи всі Run-и.</summary>
        public static string GetParagraphText(Paragraph p)
            => string.Concat(p.Descendants<Text>().Select(t => t.Text));

        /// <summary>
        /// Перевіряє, чи є абзац заголовком потрібного рівня.
        /// Підтримує стиль "Heading1"/"Heading2" та жирний текст великого розміру.
        /// </summary>
        public static bool IsHeading(Paragraph p, int level)
        {
            var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
            return styleId.Equals($"Heading{level}", StringComparison.OrdinalIgnoreCase)
                || styleId.Equals($"1", StringComparison.OrdinalIgnoreCase) && level == 1
                || styleId.Equals($"2", StringComparison.OrdinalIgnoreCase) && level == 2;
        }

        /// <summary>Читає значення з рядка таблиці за індексом клітинки (0-based).</summary>
        public static string GetCellText(TableRow row, int cellIndex)
        {
            var cells = row.Descendants<TableCell>().ToList();
            if (cellIndex >= cells.Count) return string.Empty;
            return string.Concat(cells[cellIndex].Descendants<Text>().Select(t => t.Text)).Trim();
        }
    }
}

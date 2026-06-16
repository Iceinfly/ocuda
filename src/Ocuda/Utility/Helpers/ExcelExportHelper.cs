using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Ocuda.Models;
using Ocuda.Utility.Exceptions;

namespace Ocuda.Utility.Helpers
{
    public static class ExcelExportHelper
    {
        public static readonly string ExcelFileExtension = "xlsx";

        public static readonly string ExcelMimeType
            = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static readonly string SheetCriteriaDefault = "Criteria";
        public static readonly string SheetDefault = "Sheet";
        public static readonly string SheetReportDefault = "Report";
        private const int ExcelPaddingCharacters = 1;
        private const int ExcelStyleIndexBold = 1;

        /// <summary>
        /// <para>
        /// Generate an Excel Workbook with one sheet for each of the StoredReport objects provided.
        /// </para>
        /// <para>
        /// Use this in an ASP.NET Controller like so, note the MemoryStream does not need to be
        /// disposed:
        /// </para>
        /// <para>
        /// var memoryStream = ExcelExport.GenerateWorkbook(reportSet.Reports);
        /// return new FileStreamResult(memoryStream, ExcelExport.ExcelMimeType)
        /// {
        ///     FileDownloadName = $"{Filename}.{ExcelExport.ExcelFileExtension}"
        /// };
        /// </para>
        /// </summary>
        /// <param name="reports">A collection of StoredReport objects to be exported; each item
        /// will be on its own worksheet</param>
        /// <returns>A MemoryStream of the Excel sheet to be saved or served out as an
        /// IActionResult. This MemoryStream does not need to be Disposed with .Dispose().</returns>
        public static System.IO.MemoryStream GenerateWorkbook(IEnumerable<DisplayReport> reports)
        {
            return GenerateWorkbook(reports, null, null);
        }

        /// <summary>
        /// <para>
        /// Generate an Excel Workbook with one sheet for each of the StoredReport objects provided.
        /// Provide a final sheet with the contents of the provided dictionary.
        /// </para>
        /// <para>
        /// Use this in an ASP.NET Controller like so, note the MemoryStream does not need to be
        /// disposed:
        /// </para>
        /// <para>
        /// var memoryStream = ExcelExport.GenerateWorkbook(reportSet.Reports,
        ///     criteriaDictionary,
        ///     "Report Criteria");
        /// return new FileStreamResult(memoryStream, ExcelExport.ExcelMimeType)
        /// {
        ///     FileDownloadName = $"{Filename}.{ExcelExport.ExcelFileExtension}"
        /// };
        /// </para>
        /// </summary>
        /// <param name="reports">A collection of StoredReport objects to be exported; each item
        /// will be on its own worksheet</param>
        /// <returns>A MemoryStream of the Excel sheet to be saved or served out as an
        /// IActionResult. This MemoryStream does not need to be Disposed with .Dispose().</returns>
        public static System.IO.MemoryStream GenerateWorkbook(IEnumerable<DisplayReport> reports,
            IDictionary<string, object> criteriaDictionary,
            string criteriaSheetName)
        {
            ArgumentNullException.ThrowIfNull(reports);

            var usedSheetNames = new List<string>();

            var ms = new System.IO.MemoryStream();
            using (var workbook = SpreadsheetDocument.Create(ms,
                SpreadsheetDocumentType.Workbook,
                false))
            {
                workbook.AddWorkbookPart();
                workbook.WorkbookPart.Workbook = new Workbook
                {
                    Sheets = new Sheets()
                };

                var stylesPart = workbook.WorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = GetStylesheet();
                stylesPart.Stylesheet.Save();

                foreach (var report in reports)
                {
                    var sheetPart = workbook.WorkbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    sheetPart.Worksheet = new Worksheet(sheetData);

                    var sheets = workbook.WorkbookPart.Workbook.GetFirstChild<Sheets>();
                    var relationshipId = workbook.WorkbookPart.GetIdOfPart(sheetPart);

                    uint sheetId = 1;
                    if (sheets.Elements<Sheet>().Any())
                    {
                        sheetId = sheets.Elements<Sheet>()
                            .Max(_ => _.SheetId.Value) + 1;
                    }

                    string sheetName = SanitizeSheetName(report.Title ?? SheetReportDefault,
                        usedSheetNames);

                    var sheet = new Sheet
                    {
                        Id = relationshipId,
                        SheetId = sheetId,
                        Name = sheetName
                    };
                    usedSheetNames.Add(sheetName);
                    sheets.Append(sheet);

                    var maximumColumnWidth = new Dictionary<int, int>();

                    if (report.HeaderRow != null)
                    {
                        var headerRow = new Row();
                        int columnNumber = 0;
                        foreach (var dataItem in report.HeaderRow)
                        {
                            (var cell, var length) = CreateCell(dataItem);
                            cell.StyleIndex = ExcelStyleIndexBold;
                            headerRow.AppendChild(cell);
                            if (maximumColumnWidth.TryGetValue(columnNumber, out int value))
                            {
                                maximumColumnWidth[columnNumber]
                                    = Math.Max(value, length);
                            }
                            else
                            {
                                maximumColumnWidth.Add(columnNumber, length);
                            }
                            columnNumber++;
                        }
                        sheetData.Append(headerRow);
                    }

                    foreach (var resultRow in report.Data)
                    {
                        var row = new Row();
                        int columnNumber = 0;
                        foreach (var resultItem in resultRow)
                        {
                            (var cell, var length) = CreateCell(resultItem ?? string.Empty);
                            row.AppendChild(cell);
                            if (maximumColumnWidth.TryGetValue(columnNumber, out int value))
                            {
                                maximumColumnWidth[columnNumber]
                                    = Math.Max(value, length);
                            }
                            else
                            {
                                maximumColumnWidth.Add(columnNumber, length);
                            }
                            columnNumber++;
                        }
                        sheetData.Append(row);
                    }

                    if (report.FooterRow != null)
                    {
                        var footerRow = new Row();
                        int columnNumber = 0;
                        foreach (var dataItem in report.FooterRow)
                        {
                            (var cell, var length) = CreateCell(dataItem);
                            cell.StyleIndex = ExcelStyleIndexBold;
                            footerRow.AppendChild(cell);
                            if (maximumColumnWidth.TryGetValue(columnNumber, out int value))
                            {
                                maximumColumnWidth[columnNumber] = Math.Max(value, length);
                            }
                            else
                            {
                                maximumColumnWidth.Add(columnNumber, length);
                            }
                            columnNumber++;
                        }
                        sheetData.Append(footerRow);
                    }

                    if (report.FooterText != null)
                    {
                        foreach (var dataItem in report.FooterText)
                        {
                            var footerTextRow = new Row();
                            (var cell, var length) = CreateCell(dataItem);
                            footerTextRow.AppendChild(cell);
                            sheetData.Append(footerTextRow);
                        }
                    }

                    foreach (var value in maximumColumnWidth.Keys.OrderByDescending(_ => _))
                    {
                        var columnId = value + 1;
                        var width = maximumColumnWidth[value] + ExcelPaddingCharacters;
                        Columns cs = sheet.GetFirstChild<Columns>();
                        if (cs != null)
                        {
                            var columnElements = cs.Elements<Column>()
                                .Where(_ => _.Min == columnId && _.Max == columnId);
                            if (columnElements.Any())
                            {
                                var column = columnElements.First();
                                column.Width = width;
                                column.CustomWidth = true;
                            }
                            else
                            {
                                var column = new Column
                                {
                                    Min = (uint)columnId,
                                    Max = (uint)columnId,
                                    Width = width,
                                    CustomWidth = true
                                };
                                cs.Append(column);
                            }
                        }
                        else
                        {
                            cs = new Columns();
                            cs.Append(new Column
                            {
                                Min = (uint)columnId,
                                Max = (uint)columnId,
                                Width = width,
                                CustomWidth = true
                            });
                            sheetPart.Worksheet.InsertAfter(cs,
                                sheetPart.Worksheet.GetFirstChild<SheetFormatProperties>());
                        }
                    }
                }

                if (criteriaDictionary?.Count > 0)
                {
                    var criteriaSheetPart = workbook.WorkbookPart.AddNewPart<WorksheetPart>();
                    var criteriaSheetData = new SheetData();
                    criteriaSheetPart.Worksheet = new Worksheet(criteriaSheetData);

                    var criteriaSheets = workbook.WorkbookPart.Workbook.GetFirstChild<Sheets>();
                    var criteriaRelationshipId = workbook
                        .WorkbookPart
                        .GetIdOfPart(criteriaSheetPart);

                    uint criteriaSheetId = 1;
                    if (criteriaSheets.Elements<Sheet>().Any())
                    {
                        criteriaSheetId = criteriaSheets.Elements<Sheet>()
                            .Max(_ => _.SheetId.Value) + 1;
                    }

                    var criteriaSheetNameFixed
                        = SanitizeSheetName(criteriaSheetName ?? SheetCriteriaDefault,
                            usedSheetNames);

                    var criteriaSheet = new Sheet
                    {
                        Id = criteriaRelationshipId,
                        SheetId = criteriaSheetId,
                        Name = criteriaSheetNameFixed
                    };
                    usedSheetNames.Add(criteriaSheetNameFixed);
                    criteriaSheets.Append(criteriaSheet);

                    var criteriaMaximumColumnWidth = new Dictionary<int, int>();

                    foreach (var criterion in criteriaDictionary)
                    {
                        var row = new Row();

                        (var nameCell, var nameLength) = CreateCell(criterion.Key);
                        row.AppendChild(nameCell);
                        if (criteriaMaximumColumnWidth.TryGetValue(0, out int firstColumnWidth))
                        {
                            criteriaMaximumColumnWidth[0]
                                = Math.Max(firstColumnWidth, nameLength);
                        }
                        else
                        {
                            criteriaMaximumColumnWidth.Add(0, nameLength);
                        }

                        (var dataCell, var dataLength) = CreateCell(criterion.Value);
                        row.AppendChild(dataCell);
                        if (criteriaMaximumColumnWidth.TryGetValue(1, out int columnWidth))
                        {
                            criteriaMaximumColumnWidth[1]
                                = Math.Max(columnWidth, dataLength);
                        }
                        else
                        {
                            criteriaMaximumColumnWidth.Add(1, dataLength);
                        }

                        criteriaSheetData.Append(row);
                    }

                    foreach (var value in criteriaMaximumColumnWidth.Keys.OrderByDescending(_ => _))
                    {
                        var columnId = value + 1;
                        var width = criteriaMaximumColumnWidth[value] + ExcelPaddingCharacters;
                        Columns cs = criteriaSheet.GetFirstChild<Columns>();
                        if (cs != null)
                        {
                            var columnElements = cs.Elements<Column>()
                                .Where(_ => _.Min == columnId && _.Max == columnId);
                            if (columnElements.Any())
                            {
                                var column = columnElements.First();
                                column.Width = width;
                                column.CustomWidth = true;
                            }
                            else
                            {
                                var column = new Column
                                {
                                    Min = (uint)columnId,
                                    Max = (uint)columnId,
                                    Width = width,
                                    CustomWidth = true
                                };
                                cs.Append(column);
                            }
                        }
                        else
                        {
                            cs = new Columns();
                            cs.Append(new Column
                            {
                                Min = (uint)columnId,
                                Max = (uint)columnId,
                                Width = width,
                                CustomWidth = true
                            });
                            criteriaSheetPart.Worksheet.InsertAfter(cs,
                                criteriaSheetPart.Worksheet.GetFirstChild<SheetFormatProperties>());
                        }
                    }
                }
                workbook.Save();
            }
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            return ms;
        }

        private static (Cell cell, int length) CreateCell(object dataItem)
        {
            if (dataItem is JsonElement jsonElement)
            {
                Cell cell = jsonElement.ValueKind switch
                {
                    JsonValueKind.Number => new Cell
                    {
                        CellValue = new CellValue(jsonElement.GetDecimal()),
                        DataType = (EnumValue<CellValues>)CellValues.Number
                    },
                    JsonValueKind.String => CreateStringCell(jsonElement.GetString()),
                    _ => CreateStringCell(jsonElement.ToString()),
                };
                return (cell, jsonElement.ToString().Length);
            }

            return (dataItem switch
            {
                int intVal => new Cell
                {
                    CellValue = new CellValue(intVal),
                    DataType = (EnumValue<CellValues>)CellValues.Number
                },
                long longVal => new Cell
                {
                    CellValue = new CellValue(Convert.ToDecimal(longVal)),
                    DataType = (EnumValue<CellValues>)CellValues.Number
                },
                DateTime dateTimeVal => new Cell
                {
                    CellValue = new CellValue(dateTimeVal),
                    DataType = (EnumValue<CellValues>)CellValues.Date
                },
                string stringVal => CreateStringCell(stringVal),
                _ => CreateStringCell(dataItem.ToString())
            }, dataItem.ToString().Length);
        }

        private static Cell CreateStringCell(string value)
        {
            return new Cell
            {
                CellValue = value.Length > 0 && value.All(char.IsDigit)
                    ? null
                    : new CellValue(value),
                CellFormula = value.Length > 0 && value.All(char.IsDigit)
                    ? new CellFormula($"(\"{value}\")")
                    : null,
                DataType = (EnumValue<CellValues>)CellValues.String
            };
        }

        private static Stylesheet GetStylesheet()
        {
            var stylesheet = new Stylesheet();

            var font = new Font();
            var boldFont = new Font();
            boldFont.Append(new Bold());

            var fonts = new Fonts();
            fonts.Append(font);
            fonts.Append(boldFont);

            var fill = new Fill();
            var fills = new Fills();
            fills.Append(fill);

            var border = new Border();
            var borders = new Borders();
            borders.Append(border);

            var regularFormat = new CellFormat
            {
                FontId = 0
            };
            var boldFormat = new CellFormat
            {
                FontId = 1
            };
            var cellFormats = new CellFormats();
            cellFormats.Append(regularFormat);
            cellFormats.Append(boldFormat);

            stylesheet.Append(fonts);
            stylesheet.Append(fills);
            stylesheet.Append(borders);
            stylesheet.Append(cellFormats);

            return stylesheet;
        }

        /// <summary>
        /// Generate a "safe" sheet name baed on the provided string and the list of sheet names
        /// which have already been used. This method is more strict than it needs to be (some
        /// special characters are allowed in sheet names) but it shouldn't ever generate a name
        /// that will show an error when the spreadsheet is opened.
        /// </summary>
        /// <param name="name">The suggested name to sanitize</param>
        /// <param name="usedSheetNames">A list of names which ave already been used</param>
        /// <returns>A safe and unique name to use</returns>
        /// <exception cref="OcudaException">This is thrown if there are more than 20 sheets
        /// already named <see cref="SheetDefault"/> with suffixed number and after 10 additional
        /// attempts to generate a randomly-generated sheet name</exception>
        private static string SanitizeSheetName(string name, List<string> usedSheetNames)
        {
            var stringBuilder = new StringBuilder();
            foreach (char character in name.Replace(" ", "_", StringComparison.OrdinalIgnoreCase))
            {
                if ((character >= '0' && character <= '9')
                    || (character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z')
                    || character == '-' || character == '.')
                {
                    stringBuilder.Append(character);
                }
            }

            var sheetName = stringBuilder.Length > 31
                ? stringBuilder.ToString()[..31]
                : stringBuilder.ToString();

            int counter = 1;
            while (usedSheetNames.Contains(sheetName))
            {
                sheetName = $"{SheetDefault}{counter}";
                counter++;
                if (counter > 30)
                {
                    throw new OcudaException("Unable to generate unique sheet name.");
                }
                if (counter > 20)
                {
                    sheetName = Path.GetRandomFileName();
                }
            }

            return sheetName;
        }
    }
}
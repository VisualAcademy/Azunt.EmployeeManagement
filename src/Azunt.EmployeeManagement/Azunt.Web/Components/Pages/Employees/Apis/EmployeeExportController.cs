using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Azunt.EmployeeManagement;

// Open XML SDK
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Azunt.Apis.Employees
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = "Administrators")]
    public class EmployeeExportController : ControllerBase
    {
        private readonly IEmployeeRepository _repository;

        public EmployeeExportController(IEmployeeRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Employee 목록 엑셀 다운로드 (Open XML SDK)
        /// GET /api/EmployeeExport/Excel
        /// </summary>
        [HttpGet("Excel")]
        public async Task<IActionResult> ExportToExcel()
        {
            var models = await _repository.GetAllAsync();
            if (models == null || models.Count == 0)
                return NotFound("No employee records found.");

            using var stream = new MemoryStream();

            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
            {
                // Workbook
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();

                // Styles
                var styles = wbPart.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = BuildStylesheet();
                styles.Stylesheet.Save();

                // Worksheet
                var wsPart = wbPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();

                // 컬럼 폭 (AutoFit 대체)
                var columns = new Columns(
                    new Column { Min = 2, Max = 2, Width = 12, CustomWidth = true },  // B: Id
                    new Column { Min = 3, Max = 3, Width = 28, CustomWidth = true },  // C: Name
                    new Column { Min = 4, Max = 4, Width = 22, CustomWidth = true },  // D: CreatedAt
                    new Column { Min = 5, Max = 5, Width = 12, CustomWidth = true },  // E: Active
                    new Column { Min = 6, Max = 6, Width = 24, CustomWidth = true }   // F: CreatedBy
                );

                var ws = new Worksheet();
                ws.Append(columns);
                ws.Append(sheetData);

                // 시작 위치(B2)
                const int startRow = 2;
                const int startCol = 2; // B

                // 헤더
                var headerRow = new Row { RowIndex = (uint)startRow };
                var headers = new[] { "Id", "Name", "CreatedAt", "Active", "CreatedBy" };
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.Append(CreateTextCell(ToRef(startCol + i, startRow), headers[i], styleIndex: 2)); // 2: Header
                }
                sheetData.Append(headerRow);

                // 데이터
                var currentRow = startRow + 1;
                foreach (var m in models)
                {
                    var row = new Row { RowIndex = (uint)currentRow };

                    // B: Id (숫자)
                    row.Append(CreateNumberCell(ToRef(startCol + 0, currentRow), m.Id));

                    // C: Name (문자)
                    row.Append(CreateTextCell(ToRef(startCol + 1, currentRow), m.Name ?? string.Empty));

                    // D: CreatedAt (nullable 안전 처리: Created 우선, 없으면 CreatedAt, 둘 다 없으면 빈 셀)
                    DateTimeOffset? createdAny = m.Created ?? m.CreatedAt;
                    if (createdAny.HasValue)
                    {
                        row.Append(CreateDateTimeCell(
                            ToRef(startCol + 2, currentRow),
                            createdAny.Value.ToLocalTime()));
                    }
                    else
                    {
                        // 빈 셀로 대체
                        row.Append(CreateTextCell(ToRef(startCol + 2, currentRow), string.Empty));
                    }

                    // E: Active (FALSE 시 강조 스타일 적용)
                    var isActive = m.Active ?? false;
                    var activeStyle = isActive ? (uint)1 : (uint)4; // 1: 본문, 4: 본문+연한 빨강 배경
                    row.Append(CreateTextCell(ToRef(startCol + 3, currentRow), isActive ? "TRUE" : "FALSE", activeStyle));

                    // F: CreatedBy
                    row.Append(CreateTextCell(ToRef(startCol + 4, currentRow), m.CreatedBy ?? string.Empty));

                    sheetData.Append(row);
                    currentRow++;
                }

                wsPart.Worksheet = ws;
                wsPart.Worksheet.Save();

                // Sheets
                var sheets = new Sheets();
                sheets.Append(new Sheet
                {
                    Id = wbPart.GetIdOfPart(wsPart),
                    SheetId = 1U,
                    Name = "Employees"
                });
                wbPart.Workbook.Append(sheets);
                wbPart.Workbook.Save();
            }

            var bytes = stream.ToArray();
            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_Employees.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        // =========================
        // Stylesheet
        // 0: 기본
        // 1: 본문(얇은 테두리)
        // 2: 헤더(흰 글꼴 + 파란 배경 + 테두리)
        // 3: 날짜시간(yyyy-mm-dd hh:mm:ss + 얇은 테두리)
        // 4: 본문(얇은 테두리 + 연한 빨강 배경) → Active==FALSE 강조용
        // =========================
        private static Stylesheet BuildStylesheet()
        {
            // 사용자 정의 포맷: 164 = yyyy-mm-dd hh:mm:ss
            var nfs = new NumberingFormats { Count = 1U };
            nfs.Append(new NumberingFormat
            {
                NumberFormatId = 164U,
                FormatCode = StringValue.FromString("yyyy-mm-dd hh:mm:ss")
            });

            // Fonts
            var fonts = new Fonts { Count = 2U };
            fonts.Append(new Font(new FontSize { Val = 11 }, new Color { Theme = 1 }, new FontName { Val = "Calibri" })); // 0
            fonts.Append(new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontSize { Val = 11 }, new FontName { Val = "Calibri" })); // 1

            // Fills
            var fills = new Fills { Count = 4U };
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));    // 0
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 })); // 1
            fills.Append(new Fill( // Header 파랑
                new PatternFill(
                    new ForegroundColor { Rgb = "FF1F4E79" },
                    new BackgroundColor { Indexed = 64U }
                )
                { PatternType = PatternValues.Solid })); // 2
            fills.Append(new Fill( // 연한 빨강(강조)
                new PatternFill(
                    new ForegroundColor { Rgb = "FFFFC7CE" },
                    new BackgroundColor { Indexed = 64U }
                )
                { PatternType = PatternValues.Solid })); // 3

            // Borders
            var borders = new Borders { Count = 2U };
            borders.Append(new Border()); // 0: none
            borders.Append(new Border(    // 1: thin
                new LeftBorder { Style = BorderStyleValues.Thin },
                new RightBorder { Style = BorderStyleValues.Thin },
                new TopBorder { Style = BorderStyleValues.Thin },
                new BottomBorder { Style = BorderStyleValues.Thin },
                new DiagonalBorder()
            ));

            // CellFormats
            var cfs = new CellFormats { Count = 5U };

            // 0: 기본
            cfs.Append(new CellFormat());

            // 1: 본문(테두리)
            cfs.Append(new CellFormat
            {
                BorderId = 1U,
                ApplyBorder = true
            });

            // 2: 헤더(흰 글꼴 + 파랑 배경 + 테두리)
            cfs.Append(new CellFormat
            {
                FontId = 1U,
                FillId = 2U,
                BorderId = 1U,
                ApplyFont = true,
                ApplyFill = true,
                ApplyBorder = true,
                Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center }
            });

            // 3: 날짜시간(테두리 + 사용자 포맷 164)
            cfs.Append(new CellFormat
            {
                NumberFormatId = 164U,
                ApplyNumberFormat = true,
                BorderId = 1U,
                ApplyBorder = true
            });

            // 4: 본문(테두리 + 연한 빨강 배경)
            cfs.Append(new CellFormat
            {
                BorderId = 1U,
                FillId = 3U,
                ApplyBorder = true,
                ApplyFill = true
            });

            return new Stylesheet(nfs, fonts, fills, borders, cfs);
        }

        // =========================
        // Cell helpers
        // =========================
        private static Cell CreateTextCell(string cellRef, string text, uint styleIndex = 1) =>
            new Cell
            {
                CellReference = cellRef,
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(
                    new DocumentFormat.OpenXml.Spreadsheet.Text(text ?? string.Empty)
                )
            };

        private static Cell CreateNumberCell(string cellRef, long number, uint styleIndex = 1) =>
            new Cell
            {
                CellReference = cellRef,
                StyleIndex = styleIndex,
                CellValue = new CellValue(number.ToString()),
                DataType = CellValues.Number
            };

        private static Cell CreateDateTimeCell(string cellRef, DateTimeOffset value, uint styleIndex = 3)
        {
            var oa = value.DateTime.ToOADate();
            return new Cell
            {
                CellReference = cellRef,
                StyleIndex = styleIndex,
                CellValue = new CellValue(oa.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                DataType = CellValues.Number
            };
        }

        // =========================
        // Address helpers
        // =========================
        private static string ToRef(int colIndex, int rowIndex) => $"{ToColName(colIndex)}{rowIndex}";

        private static string ToColName(int index)
        {
            var dividend = index;
            var columnName = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = (char)('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }
            return columnName;
        }
    }
}

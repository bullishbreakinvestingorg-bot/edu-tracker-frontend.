using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _db;
    public AttendanceController(ApplicationDbContext db) => _db = db;

    private IQueryable<Classroom> MyClasses()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return User.IsInRole("Admin") ? _db.Classrooms : _db.Classrooms.Where(c => c.TeacherId == uid);
    }

    // Builds the monthly table data (used by both the page and the Excel download)
    private async Task<MonthlyViewModel> BuildMonthly(int? classId, string? month)
    {
        var classes = await MyClasses().OrderBy(c => c.Name).ToListAsync();
        var selected = classes.FirstOrDefault(c => c.Id == classId) ?? classes.FirstOrDefault();

        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var parsed))
        {
            first = parsed;
        }

        var vm = new MonthlyViewModel
        {
            Classes = classes,
            ClassId = selected?.Id,
            ClassName = selected == null ? "" : $"{selected.Name} {selected.Division} - {selected.Subject}",
            MonthStart = first
        };

        if (selected != null)
        {
            var id = selected.Id;
            var end = first.AddMonths(1);

            var students = await _db.Students.Where(s => s.ClassroomId == id)
                                             .OrderBy(s => s.RollNumber).ToListAsync();

            var records = await _db.Attendances
                .Where(a => a.Student!.ClassroomId == id && a.Date >= first && a.Date < end)
                .Select(a => new { a.StudentId, a.Date, a.Status })
                .ToListAsync();

            var byStudent = records.ToLookup(r => r.StudentId);

            foreach (var s in students)
            {
                var row = new MonthlyRow { RollNumber = s.RollNumber, Name = s.FullName };
                foreach (var r in byStudent[s.Id])
                    row.Days[r.Date.Day] = r.Status;
                vm.Rows.Add(row);
            }
        }

        return vm;
    }

    // ---------- Monthly table (students x days) ----------
    [HttpGet]
    public async Task<IActionResult> Monthly(int? classId, string? month)
        => View(await BuildMonthly(classId, month));

    // ---------- Download the monthly table as a real Excel (.xlsx) file ----------
    [HttpGet]
    public async Task<IActionResult> ExportMonthly(int? classId, string? month)
    {
        var vm = await BuildMonthly(classId, month);
        if (vm.ClassId == null) return NotFound();

        var green = XLColor.FromHtml("#198754");
        var orange = XLColor.FromHtml("#E67E00");
        var red = XLColor.FromHtml("#DC3545");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(vm.MonthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture));

        // ----- Title -----
        ws.Cell(1, 1).Value = "Monthly Attendance - " + vm.ClassName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = vm.MonthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        ws.Cell(2, 1).Style.Font.Italic = true;

        // ----- Header row -----
        int headerRow = 4;
        int dayStartCol = 3;
        int lastDayCol = dayStartCol + vm.DaysInMonth - 1;
        int pCol = lastDayCol + 1, lCol = lastDayCol + 2, aCol = lastDayCol + 3, pctCol = lastDayCol + 4;

        ws.Cell(headerRow, 1).Value = "Roll";
        ws.Cell(headerRow, 2).Value = "Name";
        for (int d = 1; d <= vm.DaysInMonth; d++)
            ws.Cell(headerRow, dayStartCol + d - 1).Value = d;
        ws.Cell(headerRow, pCol).Value = "Present";
        ws.Cell(headerRow, lCol).Value = "Late";
        ws.Cell(headerRow, aCol).Value = "Absent";
        ws.Cell(headerRow, pctCol).Value = "Percent";

        var header = ws.Range(headerRow, 1, headerRow, pctCol);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D6EFD");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ----- Student rows -----
        int r = headerRow + 1;
        foreach (var row in vm.Rows)
        {
            ws.Cell(r, 1).Value = Safe(row.RollNumber);
            ws.Cell(r, 2).Value = Safe(row.Name);

            for (int d = 1; d <= vm.DaysInMonth; d++)
            {
                var cell = ws.Cell(r, dayStartCol + d - 1);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (row.Days.TryGetValue(d, out var st))
                {
                    cell.Value = st == AttendanceStatus.Present ? "P" : st == AttendanceStatus.Late ? "L" : "A";
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = st == AttendanceStatus.Present ? green
                                              : st == AttendanceStatus.Late ? orange : red;
                }
            }

            ws.Cell(r, pCol).Value = row.Present;
            ws.Cell(r, lCol).Value = row.Late;
            ws.Cell(r, aCol).Value = row.Absent;

            if (row.Percent.HasValue)
            {
                var pc = ws.Cell(r, pctCol);
                pc.Value = row.Percent.Value / 100.0;
                pc.Style.NumberFormat.Format = "0.0%";
                pc.Style.Font.Bold = true;
                pc.Style.Font.FontColor = row.Percent.Value < 75 ? red : row.Percent.Value < 85 ? orange : green;
            }

            ws.Range(r, pCol, r, pctCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            r++;
        }

        // ----- Grey weekend columns -----
        for (int d = 1; d <= vm.DaysInMonth; d++)
        {
            var dt = new DateTime(vm.MonthStart.Year, vm.MonthStart.Month, d);
            if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                ws.Range(headerRow + 1, dayStartCol + d - 1, Math.Max(r - 1, headerRow + 1), dayStartCol + d - 1)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
        }

        // ----- Borders, widths, freeze panes -----
        var table = ws.Range(headerRow, 1, Math.Max(r - 1, headerRow), pctCol);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 26;
        for (int c = dayStartCol; c <= lastDayCol; c++) ws.Column(c).Width = 4.5;
        for (int c = pCol; c <= pctCol; c++) ws.Column(c).Width = 9;
        ws.SheetView.FreezeRows(headerRow);
        ws.SheetView.FreezeColumns(2);

        // ----- Legend -----
        ws.Cell(r + 1, 1).Value = "P = Present, L = Late, A = Absent, blank = not recorded";
        ws.Cell(r + 1, 1).Style.Font.Italic = true;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var safeName = Regex.Replace(vm.ClassName, "[^A-Za-z0-9]+", "_").Trim('_');
        var fileName = $"Attendance_{safeName}_{vm.MonthStart:yyyy-MM}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // Stops text starting with = + - @ from being read as an Excel formula
    private static string Safe(string value)
    {
        value ??= "";
        return value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
    }

    // ---------- Saved attendance list (date-wise) ----------
    [HttpGet]
    public async Task<IActionResult> History(int? classId)
    {
        var classes = await MyClasses().OrderBy(c => c.Name).ToListAsync();
        var selected = classes.FirstOrDefault(c => c.Id == classId) ?? classes.FirstOrDefault();

        var vm = new HistoryViewModel
        {
            Classes = classes,
            ClassId = selected?.Id,
            ClassName = selected == null ? "" : $"{selected.Name} {selected.Division} - {selected.Subject}"
        };

        if (selected != null)
        {
            var id = selected.Id;
            vm.Rows = await _db.Attendances
                .Where(a => a.Student!.ClassroomId == id)
                .GroupBy(a => a.Date)
                .Select(g => new HistoryRow
                {
                    Date = g.Key,
                    Present = g.Count(a => a.Status == AttendanceStatus.Present),
                    Late = g.Count(a => a.Status == AttendanceStatus.Late),
                    Absent = g.Count(a => a.Status == AttendanceStatus.Absent)
                })
                .OrderByDescending(r => r.Date)
                .ToListAsync();
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Mark(int classId, DateTime? date)
    {
        var cls = await MyClasses().FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return NotFound();

        var day = (date ?? DateTime.Today).Date;
        var students = await _db.Students.Where(s => s.ClassroomId == classId)
                                         .OrderBy(s => s.RollNumber).ToListAsync();

        var existing = await _db.Attendances
            .Where(a => a.Date == day && a.Student!.ClassroomId == classId)
            .ToDictionaryAsync(a => a.StudentId, a => a.Status);

        var vm = new MarkViewModel
        {
            ClassId = classId,
            ClassName = $"{cls.Name} {cls.Division} - {cls.Subject}",
            Date = day,
            Items = students.Select(s => new MarkItem
            {
                StudentId = s.Id,
                RollNumber = s.RollNumber,
                Name = s.FullName,
                Status = existing.TryGetValue(s.Id, out var st) ? st : AttendanceStatus.Present
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(MarkViewModel vm)
    {
        var cls = await MyClasses().FirstOrDefaultAsync(c => c.Id == vm.ClassId);
        if (cls == null) return NotFound();

        var day = vm.Date.Date;
        var validIds = await _db.Students.Where(s => s.ClassroomId == vm.ClassId)
                                         .Select(s => s.Id).ToListAsync();
        var existing = await _db.Attendances
            .Where(a => a.Date == day && validIds.Contains(a.StudentId)).ToListAsync();

        foreach (var item in vm.Items.Where(i => validIds.Contains(i.StudentId)))
        {
            var row = existing.FirstOrDefault(a => a.StudentId == item.StudentId);
            if (row == null)
                _db.Attendances.Add(new Attendance { Date = day, StudentId = item.StudentId, Status = item.Status });
            else
                row.Status = item.Status;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Attendance saved for {day:dd MMM yyyy}.";
        return RedirectToAction(nameof(Mark), new { classId = vm.ClassId, date = day.ToString("yyyy-MM-dd") });
    }
}
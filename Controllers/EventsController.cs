
using EduTrack.Data;
using EduTrack.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;

namespace EduTrack.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;

    public EventsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private IQueryable<Classroom> MyClasses()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (User.IsInRole("Admin"))
            return _db.Classrooms;

        return _db.Classrooms.Where(
            c => c.TeacherId == userId);
    }

    // Display events
    public async Task<IActionResult> Index()
    {
        var classes = await MyClasses()
            .Select(c => c.Id)
            .ToListAsync();

        var events = await _db.Events
            .Where(e => classes.Contains(e.ClassroomId))
            .Include(e => e.Classroom)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        return View(events);
    }

    // Display event creation page
    public async Task<IActionResult> Create()
    {
        await LoadClasses();
        return View();
    }

    // Create event
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event model)
    {
        var classroom = await MyClasses()
            .FirstOrDefaultAsync(
                c => c.Id == model.ClassroomId);

        if (classroom == null)
        {
            ModelState.AddModelError(
                "ClassroomId",
                "Please select a valid class.");
        }

        if (ModelState.IsValid)
        {
            model.CreatedById =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "";

            _db.Events.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Event created successfully.";

            return RedirectToAction(nameof(Index));
        }

        await LoadClasses();
        return View(model);
    }

    // Display event attendance page
    public async Task<IActionResult> Attendance(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Classroom)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
            return NotFound();

        var allowed = await MyClasses()
            .AnyAsync(c => c.Id == ev.ClassroomId);

        if (!allowed)
            return Forbid();

        var students = await _db.Students
            .Where(s => s.ClassroomId == ev.ClassroomId)
            .OrderBy(s => s.RollNumber)
            .ToListAsync();

        var participations = await _db.EventParticipations
            .Where(p => p.EventId == id)
            .ToDictionaryAsync(
                p => p.StudentId,
                p => p.IsPresent);

        ViewBag.Event = ev;
        ViewBag.Participations = participations;

        return View(students);
    }

    // Save event attendance
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attendance(
        int id, int[] presentStudentIds)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
            return NotFound();

        var allowed = await MyClasses()
            .AnyAsync(c => c.Id == ev.ClassroomId);

        if (!allowed)
            return Forbid();

        var studentIds = await _db.Students
            .Where(s => s.ClassroomId == ev.ClassroomId)
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await _db.EventParticipations
            .Where(p => p.EventId == id)
            .ToListAsync();

        foreach (var studentId in studentIds)
        {
            var participation = existing
                .FirstOrDefault(
                    p => p.StudentId == studentId);

            var isPresent =
                presentStudentIds.Contains(studentId);

            if (participation == null)
            {
                _db.EventParticipations.Add(
                    new EventParticipation
                    {
                        EventId = id,
                        StudentId = studentId,
                        IsPresent = isPresent
                    });
            }
            else
            {
                participation.IsPresent = isPresent;
            }
        }

        await _db.SaveChangesAsync();

        TempData["Success"] =
            "Event attendance saved.";

        return RedirectToAction(nameof(Index));
    }

    // Download event attendance as Excel
    public async Task<IActionResult> DownloadAttendance(int id)
    {
        // Find event
        var ev = await _db.Events
            .Include(e => e.Classroom)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
            return NotFound();

        // Check class permissions
        var allowed = await MyClasses()
            .AnyAsync(c => c.Id == ev.ClassroomId);

        if (!allowed)
            return Forbid();

        // Get students in the event's class
        var students = await _db.Students
            .Where(s => s.ClassroomId == ev.ClassroomId)
            .OrderBy(s => s.RollNumber)
            .ToListAsync();

        // Get saved attendance
        var participations = await _db.EventParticipations
            .Where(p => p.EventId == id)
            .ToDictionaryAsync(
                p => p.StudentId,
                p => p.IsPresent);

        // Create Excel workbook
        using var workbook = new XLWorkbook();

        var worksheet = workbook.Worksheets.Add(
            "Event Attendance");

        // Headings
        worksheet.Cell(1, 1).Value = "Event Name";
        worksheet.Cell(1, 2).Value = "Event Date";
        worksheet.Cell(1, 3).Value = "Class";
        worksheet.Cell(1, 4).Value = "Roll Number";
        worksheet.Cell(1, 5).Value = "Student Name";
        worksheet.Cell(1, 6).Value = "Attendance";

        // Format headings
        var header = worksheet.Range(1, 1, 1, 6);

        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor =
            XLColor.LightBlue;

        header.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        // Add attendance records
        int row = 2;

        foreach (var student in students)
        {
            bool isPresent =
                participations.TryGetValue(
                    student.Id, out var status) && status;

            worksheet.Cell(row, 1).Value = ev.Name;

            worksheet.Cell(row, 2).Value = ev.Date;
            worksheet.Cell(row, 2).Style.DateFormat.Format =
                "dd MMM yyyy";

            worksheet.Cell(row, 3).Value =
                ev.Classroom?.Name ?? "";

            worksheet.Cell(row, 4).Value =
                student.RollNumber;

            worksheet.Cell(row, 5).Value =
                student.FullName;

            // Missing attendance records count as Absent
            worksheet.Cell(row, 6).Value =
                isPresent ? "Present" : "Absent";

            row++;
        }

        // Adjust column widths
        worksheet.Columns().AdjustToContents();

        // Add borders
        if (row > 1)
        {
            worksheet.Range(1, 1, row - 1, 6)
                .Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

            worksheet.Range(1, 1, row - 1, 6)
                .Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;
        }

        // Generate Excel file
        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"EventAttendance_{ev.Id}.xlsx");
    }

    // Load classes for event creation
    private async Task LoadClasses()
    {
        var classes = await MyClasses()
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Classrooms = new SelectList(
            classes, "Id", "Name");
    }
}

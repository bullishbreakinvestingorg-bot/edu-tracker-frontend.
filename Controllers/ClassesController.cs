using System.Security.Claims;
using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers;

[Authorize]
public class ClassesController : Controller
{
    private readonly ApplicationDbContext _db;
    public ClassesController(ApplicationDbContext db) => _db = db;

    private IQueryable<Classroom> MyClasses()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return User.IsInRole("Admin") ? _db.Classrooms : _db.Classrooms.Where(c => c.TeacherId == uid);
    }

    public async Task<IActionResult> Index()
    {
        var list = await MyClasses().Include(c => c.Students).OrderBy(c => c.Name).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? division, string subject, string academicYear)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(subject))
        {
            ViewBag.Error = "Class name and subject are required.";
            return View();
        }

        _db.Classrooms.Add(new Classroom
        {
            Name = name.Trim(),
            Division = division?.Trim(),
            Subject = subject.Trim(),
            AcademicYear = string.IsNullOrWhiteSpace(academicYear) ? "2026-27" : academicYear.Trim(),
            TeacherId = User.FindFirstValue(ClaimTypes.NameIdentifier)!
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var cls = await MyClasses().Include(c => c.Students.OrderBy(s => s.RollNumber))
                                   .FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return NotFound();
        return View(cls);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStudent(int classId, string firstName, string? lastName, string rollNumber)
    {
        var cls = await MyClasses().FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return NotFound();

        var roll = (rollNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(firstName) || roll == "")
        {
            TempData["Error"] = "First name and roll number are required.";
        }
        else if (await _db.Students.AnyAsync(s => s.RollNumber == roll))
        {
            TempData["Error"] = $"Roll number {roll} already exists.";
        }
        else
        {
            _db.Students.Add(new Student
            {
                FirstName = firstName.Trim(),
                LastName = (lastName ?? "").Trim(),
                RollNumber = roll,
                ClassroomId = classId
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Student added.";
        }
        return RedirectToAction(nameof(Details), new { id = classId });
    }
}
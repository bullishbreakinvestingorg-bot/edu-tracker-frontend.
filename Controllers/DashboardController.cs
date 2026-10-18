using System.Security.Claims;
using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private const double AtRiskLimit = 75.0;
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var stats = await _db.Students
            .Where(s => isAdmin || s.Classroom!.TeacherId == userId)
            .Select(s => new StudentStat
            {
                Name = s.FirstName + " " + s.LastName,
                ClassName = s.Classroom!.Name + " " + (s.Classroom.Division ?? ""),
                Total = s.Attendance.Count(),
                Attended = s.Attendance.Count(a => a.Status != AttendanceStatus.Absent)
            })
            .ToListAsync();

        var withData = stats.Where(s => s.Percent.HasValue).ToList();

        var vm = new DashboardViewModel
        {
            TotalStudents = stats.Count,
            TotalClasses = await _db.Classrooms.CountAsync(c => isAdmin || c.TeacherId == userId),
            AverageAttendance = withData.Count == 0 ? 0 : Math.Round(withData.Average(s => s.Percent!.Value), 1),
            AtRisk = withData.Where(s => s.Percent < AtRiskLimit).OrderBy(s => s.Percent).ToList(),
            ClassAverages = withData.GroupBy(s => s.ClassName)
                .Select(g => new ClassAvg { Name = g.Key, Average = Math.Round(g.Average(s => s.Percent!.Value), 1) })
                .ToList(),
            RecentEvents = await _db.Events.OrderByDescending(e => e.Date).Take(5).ToListAsync()
        };

        return View(vm);
    }
}
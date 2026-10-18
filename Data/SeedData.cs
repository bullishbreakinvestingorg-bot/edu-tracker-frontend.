
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();

        // Create Admin role
        if (!await roles.RoleExistsAsync("Admin"))
        {
            await roles.CreateAsync(new IdentityRole("Admin"));
        }

        // Create Admin user
        var admin = await EnsureUser(
            users,
            "admin@college.edu",
            "Admin@123"
        );

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            await users.AddToRoleAsync(admin, "Admin");
        }

        // Create Teacher user
        var teacher = await EnsureUser(
            users,
            "teacher@college.edu",
            "Teacher@123"
        );

        // Stop if classroom data already exists
        if (await db.Classrooms.AnyAsync())
        {
            return;
        }

        // Create sample classroom
        var cls = new Classroom
        {
            Name = "TY CSE",
            Division = "A",
            Subject = "Python",
            TeacherId = teacher.Id
        };

        // Create sample students
        var names = new[]
        {
            "Aarav Patil",
            "Isha Kulkarni",
            "Rohan Deshmukh",
            "Sneha Jadhav",
            "Karan More",
            "Pooja Shinde"
        };

        int roll = 101;

        foreach (var name in names)
        {
            var parts = name.Split(' ');

            cls.Students.Add(new Student
            {
                FirstName = parts[0],
                LastName = parts[1],
                RollNumber = "CSE" + roll++
            });
        }

        db.Classrooms.Add(cls);
        await db.SaveChangesAsync();

        // Create sample regular attendance
        var random = new Random(42);

        var days = Enumerable.Range(1, 30)
            .Select(i => DateTime.Today.AddDays(-i))
            .Where(d =>
                d.DayOfWeek != DayOfWeek.Saturday &&
                d.DayOfWeek != DayOfWeek.Sunday)
            .ToList();

        foreach (var student in cls.Students)
        {
            double absentChance =
                student.RollNumber == "CSE103" ||
                student.RollNumber == "CSE105"
                    ? 0.35
                    : 0.08;

            foreach (var date in days)
            {
                var value = random.NextDouble();

                var status =
                    value < absentChance
                        ? AttendanceStatus.Absent
                        : value < absentChance + 0.05
                            ? AttendanceStatus.Late
                            : AttendanceStatus.Present;

                db.Attendances.Add(new Attendance
                {
                    Date = date,
                    StudentId = student.Id,
                    Status = status
                });
            }
        }

        // Create sample events without points
        db.Events.Add(new Event
        {
            Name = "Hackathon 2026",
            Date = DateTime.Today.AddDays(-20),
            CreatedById = teacher.Id,
            ClassroomId = cls.Id
        });

        db.Events.Add(new Event
        {
            Name = "Tech Quiz",
            Date = DateTime.Today.AddDays(-8),
            CreatedById = teacher.Id,
            ClassroomId = cls.Id
        });

        await db.SaveChangesAsync();
    }

    private static async Task<IdentityUser> EnsureUser(
        UserManager<IdentityUser> userManager,
        string email,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user != null)
        {
            return user;
        }

        user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(e => e.Description)
            );

            throw new InvalidOperationException(
                $"Could not create user {email}: {errors}"
            );
        }

        return user;
    }
}
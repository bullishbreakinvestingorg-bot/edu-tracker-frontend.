using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models;

public class Classroom
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(10)]
    public string? Division { get; set; }

    [Required, StringLength(100)]
    public string Subject { get; set; } = "";

    [StringLength(9)]
    public string AcademicYear { get; set; } = "2026-27";

    public string TeacherId { get; set; } = "";

    public List<Student> Students { get; set; } = new();
}

public class Student
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string FirstName { get; set; } = "";

    [StringLength(50)]
    public string LastName { get; set; } = "";

    [Required, StringLength(20)]
    public string RollNumber { get; set; } = "";

    public int ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }

    public List<Attendance> Attendance { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Late = 2
}

public class Attendance
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }
}

public class Event
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    public DateTime Date { get; set; }

    public string CreatedById { get; set; } = "";

    public int ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }

    public List<EventParticipation> Participants { get; set; } = new();
}

public class EventParticipation
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public bool IsPresent { get; set; }
}


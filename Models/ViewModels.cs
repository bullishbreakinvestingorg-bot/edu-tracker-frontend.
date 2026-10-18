namespace EduTrack.Models;

public class StudentStat
{
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int Total { get; set; }
    public int Attended { get; set; }

    public double? Percent => Total == 0 ? null : Attended * 100.0 / Total;
}

public class ClassAvg
{
    public string Name { get; set; } = "";
    public double Average { get; set; }
}

public class DashboardViewModel
{
    public int TotalStudents { get; set; }
    public int TotalClasses { get; set; }
    public double AverageAttendance { get; set; }
    public List<StudentStat> AtRisk { get; set; } = new();
    public List<ClassAvg> ClassAverages { get; set; } = new();
    public List<Event> RecentEvents { get; set; } = new();
}

public class MarkItem
{
    public int StudentId { get; set; }
    public string RollNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public AttendanceStatus Status { get; set; }
}

public class MarkViewModel
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public DateTime Date { get; set; }
    public List<MarkItem> Items { get; set; } = new();
}

// ---------- Saved Attendance (date-wise list) ----------
public class HistoryRow
{
    public DateTime Date { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }

    public int Total => Present + Late + Absent;
    public double Percent => Total == 0 ? 0 : (Present + Late) * 100.0 / Total;
}

public class HistoryViewModel
{
    public List<Classroom> Classes { get; set; } = new();
    public int? ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public List<HistoryRow> Rows { get; set; } = new();
}

// ---------- NEW: Monthly attendance table ----------
public class MonthlyRow
{
    public string RollNumber { get; set; } = "";
    public string Name { get; set; } = "";

    // day of month (1-31) -> status
    public Dictionary<int, AttendanceStatus> Days { get; set; } = new();

    public int Present => Days.Values.Count(v => v == AttendanceStatus.Present);
    public int Late => Days.Values.Count(v => v == AttendanceStatus.Late);
    public int Absent => Days.Values.Count(v => v == AttendanceStatus.Absent);
    public int Total => Days.Count;

    public double? Percent => Total == 0 ? null : (Present + Late) * 100.0 / Total;
}

public class MonthlyViewModel
{
    public List<Classroom> Classes { get; set; } = new();
    public int? ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public DateTime MonthStart { get; set; }
    public int DaysInMonth => DateTime.DaysInMonth(MonthStart.Year, MonthStart.Month);
    public List<MonthlyRow> Rows { get; set; } = new();
}
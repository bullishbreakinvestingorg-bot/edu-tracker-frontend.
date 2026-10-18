
using EduTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Student roll numbers must be unique
        builder.Entity<Student>()
            .HasIndex(s => s.RollNumber)
            .IsUnique();

        // One attendance record per student per date
        builder.Entity<Attendance>()
            .HasIndex(a => new { a.Date, a.StudentId })
            .IsUnique();

        // One participation record per student per event
        builder.Entity<EventParticipation>()
            .HasIndex(p => new { p.EventId, p.StudentId })
            .IsUnique();

        // Prevent multiple cascade paths for classroom and event
        builder.Entity<Event>()
            .HasOne(e => e.Classroom)
            .WithMany()
            .HasForeignKey(e => e.ClassroomId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<LessonTemplate> LessonTemplates { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }

    // Finance Module Tables
    public DbSet<Income> Incomes { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<Debt> Debts { get; set; }
    public DbSet<Investment> Investments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Student entity configuration
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Soyad).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Telefon).HasMaxLength(20);
            entity.Property(e => e.Not).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasMany(e => e.Lessons)
                  .WithOne(e => e.Student)
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Payments)
                  .WithOne(e => e.Student)
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Lesson entity configuration
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Konu).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Ucret).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Durum).IsRequired().HasMaxLength(20).HasDefaultValue("Planlı");
            entity.Property(e => e.Not).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.Baslangic);
            entity.HasIndex(e => e.Durum);
        });

        // Payment entity configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Tutar).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Donem).IsRequired().HasMaxLength(20).HasDefaultValue("Haftalık");
            entity.Property(e => e.Aciklama).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            // Enum'ı string olarak sakla
            entity.Property(e => e.Durum)
                  .HasConversion<string>()
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(PaymentStatus.Beklemede);

            entity.HasIndex(e => e.Tarih);
        });

        // LessonTemplate entity configuration
        modelBuilder.Entity<LessonTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Konu).IsRequired().HasMaxLength(200);
            entity.Property(e => e.VarsayilanUcret).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.VarsayilanNot).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // ActivityLog entity configuration
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Income entity configuration
        modelBuilder.Entity<Income>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Tutar).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Siklik).IsRequired().HasMaxLength(20).HasDefaultValue("Bir Kerelik");
            entity.Property(e => e.Kategori).IsRequired().HasMaxLength(50).HasDefaultValue("Diğer");
            entity.Property(e => e.Aciklama).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.Tarih);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Expense entity configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Tutar).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Kategori).IsRequired().HasMaxLength(50).HasDefaultValue("Diğer");
            entity.Property(e => e.OdemeYontemi).IsRequired().HasMaxLength(50).HasDefaultValue("Nakit");
            entity.Property(e => e.Aciklama).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.Tarih);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Kategori);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Debt entity configuration
        modelBuilder.Entity<Debt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ToplamTutar).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.KalanTutar).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.AylikTaksit).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.FaizOrani).HasColumnType("decimal(5,2)").HasDefaultValue(0);
            entity.Property(e => e.BorcTipi).IsRequired().HasMaxLength(50).HasDefaultValue("Diğer");
            entity.Property(e => e.KurumKisi).HasMaxLength(100);
            entity.Property(e => e.Aciklama).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AktifMi);
            entity.HasIndex(e => e.SonrakiOdemeTarihi);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Investment entity configuration
        modelBuilder.Entity<Investment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Tip).IsRequired().HasMaxLength(50).HasDefaultValue("Diğer");
            entity.Property(e => e.Miktar).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BirimFiyat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.GuncelFiyat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Aciklama).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AktifMi);
            entity.HasIndex(e => e.Tip);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Computed columns'ları ignore et (veritabanına kaydedilmez)
            entity.Ignore(e => e.ToplamDeger);
            entity.Ignore(e => e.Kar);
            entity.Ignore(e => e.KarYuzdesi);
        });

        // Seed data (optional)
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Örnek öğrenci verisi - static datetime kullanılıyor
        modelBuilder.Entity<Student>().HasData(
            new Student
            {
                Id = 1,
                Ad = "Ahmet",
                Soyad = "Yılmaz",
                Email = "ahmet@example.com",
                Telefon = "05551234567",
                AktifMi = true,
                CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0)
            }
        );
    }
}

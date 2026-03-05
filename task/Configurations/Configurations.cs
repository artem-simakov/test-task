using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using task.Entities;

namespace task.Configurations;

public class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("offices");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Uuid).HasMaxLength(50);
        builder.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.AddressRegion).HasMaxLength(255);
        builder.Property(x => x.AddressCity).HasMaxLength(255);
        builder.Property(x => x.AddressStreet).HasMaxLength(255);
        builder.Property(x => x.AddressHouseNumber).HasMaxLength(50);
        builder.Property(x => x.WorkTime).HasMaxLength(500);

        builder.OwnsOne(x => x.Coordinates, cb =>
        {
            cb.Property(c => c.Latitude).HasColumnName("latitude");
            cb.Property(c => c.Longitude).HasColumnName("longitude");
        });

        builder.HasMany(x => x.Phones)
               .WithOne(p => p.Office)
               .HasForeignKey(p => p.OfficeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Code);
        builder.HasIndex(x => x.Uuid);
        builder.HasIndex(x => x.CityCode);
    }
}

public class PhoneConfiguration : IEntityTypeConfiguration<Phone>
{
    public void Configure(EntityTypeBuilder<Phone> builder)
    {
        builder.ToTable("phones");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.PhoneNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Additional).HasMaxLength(255);

        builder.HasIndex(x => x.OfficeId);
    }
}

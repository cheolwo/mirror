using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Data.Configurations.Food;

public sealed class 음식운영정책Configuration : IEntityTypeConfiguration<음식운영정책>
{
    public void Configure(EntityTypeBuilder<음식운영정책> builder)
    {
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.기사기상할증정책판본).HasMaxLength(100);
    }
}

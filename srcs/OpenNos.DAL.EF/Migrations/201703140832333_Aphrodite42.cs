using System.Data.Entity.Migrations;

namespace OpenNos.DAL.EF.Migrations
{
    public partial class Aphrodite42 : DbMigration
    {
        #region Methods

        public override void Down()
        {
            DropForeignKey("dbo.TimeSpace", "MapId", "dbo.Map");
            DropIndex("dbo.TimeSpace", new[] { "MapId" });
            DropTable("dbo.TimeSpace");
        }

        public override void Up()
        {
            CreateTable(
                    "dbo.TimeSpace",
                    c => new
                    {
                        TimespaceId = c.Short(false, true),
                        MapId = c.Short(false),
                        PositionX = c.Short(false),
                        PositionY = c.Short(false),
                        LevelMinimum = c.Int(false),
                        LevelMaximum = c.Int(false),
                        Winner = c.String(),
                        DrawItemGift = c.String(),
                        BonusItemGift = c.String(),
                        SpecialItemGift = c.String(),
                        Label = c.String(),
                        WinnerScore = c.Int(false)
                    })
                .PrimaryKey(t => t.TimespaceId)
                .ForeignKey("dbo.Map", t => t.MapId)
                .Index(t => t.MapId);
        }

        #endregion
    }
}
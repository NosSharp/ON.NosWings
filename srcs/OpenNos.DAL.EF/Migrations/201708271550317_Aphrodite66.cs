using System.Data.Entity.Migrations;

namespace OpenNos.DAL.EF.Migrations
{
    public partial class Aphrodite66 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ScriptedInstance", "Label", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.ScriptedInstance", "Label");
        }
    }
}
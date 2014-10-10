using System.Data.Entity;

namespace AOFBatchTest.Models
{
    public class ContextModel : DbContext
    {
        public ContextModel()
        {
            System.Data.Entity.Database.SetInitializer<ContextModel>(null);//new DropCreateDatabaseAlways<ContextModel>());
        }
        public DbSet<InvoicePrerequisite> InvoicePrerequisites { get; set; }
        public DbSet<AofFile> AofFiles { get; set; }
    }
}
using System;

namespace AOFBatchTest.Models
{
    public class AofFile
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public byte[] Content { get; set; }
    }
}
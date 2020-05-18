using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSM
{
    public class Review
    {
        public string GoodsId { set; get; }
        public string ReviewId { set; get;}
        public string ReviewName { set; get; }
        public long TotalLikeCnt { set; get; }
        public long LikeCnt { set; get; }
        public long TotalCollCnt { set; get; }
        public long CollCnt { set; get; }
        public long TotalCommCnt { set; get; }
        public long CommCnt { set; get; }
        public string RegDt { set; get; }
        public string Status { set; get; }
    }
}

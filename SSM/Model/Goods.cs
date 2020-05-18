using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSM
{
    public class Goods
    {
        public string GoodsId { set; get;}
        public string GoodsName { set; get; }
        public long MinLikeCnt { set; get; }
        public long MinCollCnt { set; get; }
        public long MinCommCnt { set; get; }
        public long MaxLikeCnt { set; get; }
        public long MaxCollCnt { set; get; }
        public long MaxCommCnt { set; get; }
        public string Memo { set; get; }
    }
}

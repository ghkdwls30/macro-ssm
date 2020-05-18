using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSM.Util
{
    class CommonUtil
    {

        public static int MakeRandomValue(int min, int max)
        {
            Random random = new Random(Guid.NewGuid().GetHashCode());
            return random.Next(min, max + 1); // 1 <= x < 100
        }

        public static int MakeRandomValue(int min, int max, List<int> excludePickList)
        {
            if (min > max) {
                int temp = max;
                max = min;
                min = temp;
            }

            int result = 0;
            while (true)
            {
                result = MakeRandomValue(min, max);
                if (excludePickList.Contains(result))continue;
                break;
            }
            return result;
        }
    }
}

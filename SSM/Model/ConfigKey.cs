using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSM.Model
{
    public class ConfigKey
    {
        //리뷰 편입 설정 키
        public static string CONFIG_REVIEW_PRIOD = "CONFIG_REVIEW_PRIOD";
        public static string CONFIG_MIN_REVIEW_SEARCH_TIME = "CONFIG_MIN_REVIEW_SEARCH_TIME";
        public static string CONFIG_MAX_REVIEW_SEARCH_TIME = "CONFIG_MAX_REVIEW_SEARCH_TIME";

        // 작업 설정

        public static string CONFIG_MIN_EACH_REVIEW_WAIT_TIME = "CONFIG_MIN_EACH_REVIEW_WAIT_TIME";
        public static string CONFIG_MAX_EACH_REVIEW_WAIT_TIME = "CONFIG_MAX_EACH_REVIEW_WAIT_TIME";


        public static string CONFIG_MIN_CYCLE_REVIEW_WAIT_TIME = "CONFIG_MIN_CYCLE_REVIEW_WAIT_TIME";
        public static string CONFIG_MAX_CYCLE_REVIEW_WAIT_TIME = "CONFIG_MAX_CYCLE_REVIEW_WAIT_TIME";

        public static string CONFIG_MIN_CYCLE_WAIT_TIME = "CONFIG_MIN_CYCLE_WAIT_TIME";
        public static string CONFIG_MAX_CYCLE_WAIT_TIME = "CONFIG_MAX_CYCLE_WAIT_TIME";

        public static string CONFIG_MIN_CYCLE_REVIEW_WORK_COUNT = "CONFIG_MIN_CYCLE_REVIEW_WORK_COUNT";
        public static string CONFIG_MAX_CYCLE_REVIEW_WORK_COUNT = "CONFIG_MAX_CYCLE_REVIEW_WORK_COUNT";
    }
}

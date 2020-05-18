using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace SSM
{
    class SqlUtil
    {
        readonly string ConnectionString;
      

        public SQLiteConnection ConnectionToDB()
        {             
            SQLiteConnection dbConnection = new SQLiteConnection("Data Source=./Repository/Database.db;Version=3;");
            dbConnection.Open();
            return dbConnection;
        }

        public void DisconnectionToDB(SQLiteConnection dbConnection)
        {
            if (dbConnection != null)
            {
                dbConnection.Close();
            }
        }

        // 모든 상품 리스트
        public List<Goods> SelectAllGoods()
        {
            List<Goods> list = new List<Goods>();

            SQLiteConnection dbConnection = ConnectionToDB();
            
            string sql = "SELECT * FROM GOODS";

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                Goods g = new Goods();
                g.GoodsId = (string)reader["GoodsId"];
                g.GoodsName = (string)reader["GoodsName"];
                g.MinCollCnt = (long)reader["MinCollCnt"];
                g.MinLikeCnt = (long)reader["MinLikeCnt"];
                g.MinCommCnt = (long)reader["MinCommCnt"];

                g.MaxCollCnt = (long)reader["MaxCollCnt"];
                g.MaxLikeCnt = (long)reader["MaxLikeCnt"];
                g.MaxCommCnt = (long)reader["MaxCommCnt"];

                g.Memo = (string)reader["Memo"];

                list.Add(g);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }

        // 상품키에 해당하는 모든 리뷰리스트 조회
        public List<Review> SelectAllReviewByGoodsId(string goodsId)
        {
            return SelectAllReviewByGoodsId(goodsId, -1);
        }

        public List<Review> SelectAllReviewByGoodsId(string goodsId, int limitCnt)
        {
            List<Review> list = new List<Review>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = "";

            if (limitCnt != -1)
            {
                sql = string.Format("SELECT * FROM REVIEW R INNER JOIN GOODS G ON R.GOODSID = G.GOODSID WHERE R.GOODSID = '{0}' ORDER BY R.STATUS ASC, R.REGDT DESC", goodsId);
            }
            else
            {
                sql = string.Format("SELECT * FROM REVIEW R INNER JOIN GOODS G ON R.GOODSID = G.GOODSID WHERE R.GOODSID = '{0}' ORDER BY R.STATUS ASC, R.REGDT DESC LIMIT {1}", goodsId, limitCnt);
            }
            

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                Review r = new Review();
                r.GoodsId = (string)reader["GoodsId"];
                r.ReviewId = (string)reader["ReviewId"];
                r.ReviewName = (string)reader["ReviewName"];
                r.CollCnt = (long)reader["CollCnt"];
                r.LikeCnt = (long)reader["LikeCnt"];
                r.CommCnt = (long)reader["CommCnt"];

                r.TotalCollCnt = (long)reader["TotalCollCnt"];
                r.TotalLikeCnt = (long)reader["TotalLikeCnt"];
                r.TotalCommCnt = (long)reader["TotalCommCnt"];

                r.RegDt = (string)reader["RegDt"];
                r.Status = (string)reader["Status"];

                list.Add(r);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }


        public Dictionary<string, object> SelectAllConfigMap()
        {
            Dictionary<string, object> map = new Dictionary<string, object>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("SELECT * FROM CONFIG");

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                map.Add((string)reader["KEY"], reader["VALUE"]);
            }

            DisconnectionToDB(dbConnection);

            return map;
        }

        internal void InsertOrUpateConfigByKey(string key, string value)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("INSERT OR REPLACE INTO CONFIG (KEY, VALUE) VALUES ( '{0}', {1})", key, value);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void DeleteGoodsByGoodsId(string goodsId)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("DELETE FROM GOODS WHERE GOODSID = '{0}'", goodsId);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            sql = string.Format("DELETE FROM REVIEW WHERE GOODSID = '{0}'", goodsId);
            command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void InsertReview(Review review)
        {
            SQLiteConnection dbConnection = ConnectionToDB();
            string sql = string.Format("INSERT INTO REVIEW (GoodsId,  ReviewId, ReviewName, LikeCnt, CollCnt, CommCnt, TotalLikeCnt, TotalCollCnt, TotalCommCnt, RegDt, Status) VALUES ('{0}','{1}','{2}',{3},{4},'{5}',{6},{7},{8}, '{9}', '{10}')"
            , review.GoodsId, review.ReviewId, review.ReviewName.Replace("\'", "''"), review.LikeCnt, review.CollCnt, review.CommCnt, review.TotalLikeCnt, review.TotalCollCnt, review.TotalCommCnt, review.RegDt, review.Status);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void DeleteReviewByGoodsId(string goodsId)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("DELETE FROM REVIEW WHERE GOODSID = '{0}'", goodsId);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateAllReviewStatusResetByGoodsId(string goodsId)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE REVIEW SET STATUS = '[2]대기' WHERE GOODSID = '{0}' AND STATUS = '[1]작업중'", goodsId);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal List<Review> SelectStageReviewByGoodsId(string goodsId)
        {
            List<Review> list = new List<Review>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("SELECT * FROM REVIEW R INNER JOIN GOODS G ON R.GOODSID = G.GOODSID WHERE R.GOODSID = {0} AND R.STATUS ='[2]대기' OR R.STATUS = '[1]작업중' ORDER BY R.STATUS ASC, R.REGDT ASC", goodsId);

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                Review r = new Review();
                r.GoodsId = (string)reader["GoodsId"];
                r.ReviewId = (string)reader["ReviewId"];
                r.ReviewName = (string)reader["ReviewName"];
                r.CollCnt = (long)reader["CollCnt"];
                r.LikeCnt = (long)reader["LikeCnt"];
                r.CommCnt = (long)reader["CommCnt"];

                r.TotalCollCnt = (long)reader["TotalCollCnt"];
                r.TotalLikeCnt = (long)reader["TotalLikeCnt"];
                r.TotalCommCnt = (long)reader["TotalCommCnt"];

                r.RegDt = (string)reader["RegDt"];
                r.Status = (string)reader["Status"];

                list.Add(r);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }

        internal List<User> SelectUserByStatus(string status, int limit)
        {
            Console.WriteLine("SelectUserByStatus");

            List<User> list = new List<User>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("SELECT * FROM USER WHERE STATUS = '{0}' LIMIT {1}", status, limit);

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                User u = new User();
                u.id = (string)reader["Id"];
                u.pwd = (string)reader["Pwd"];
                u.nick = (string)reader["Nick"];
                u.email = (string)reader["Email"];
                u.gender = (string)reader["Gender"];
                u.status = (string)reader["Status"];
                u.year = (string)reader["Year"];
                u.month = (string)reader["Month"];
                u.day = (string)reader["Day"];
                u.imageYn = (string)reader["ImageYn"];

                list.Add(u);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }

        internal void InsertGoods(Goods goods)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("INSERT INTO GOODS (GoodsId,  GoodsName, MinLikeCnt, MinCollCnt, MinCommCnt, Memo, MaxLikeCnt, MaxCollCnt, MaxCommCnt) VALUES ('{0}','{1}',{2},{3},{4},'{5}',{6},{7},{8})"
            , goods.GoodsId, goods.GoodsName, goods.MinLikeCnt, goods.MinCollCnt, goods.MinCommCnt, goods.Memo, goods.MaxLikeCnt, goods.MaxCollCnt, goods.MaxCommCnt);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateReviewLikeCnt(string reviewId, long v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE REVIEW SET LIKECNT = {1} WHERE REVIEWID = '{0}'", reviewId, v);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateGoodsNameByGoodsId(string goodsId, string v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE GOODS SET GOODSNAME = '{1}' WHERE GOODSID = '{0}'", goodsId, v);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateReviewCommCnt(string reviewId, long v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE REVIEW SET COMMCNT = {1} WHERE REVIEWID = '{0}'", reviewId, v);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateReviewCollCnt(string reviewId, long v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE REVIEW SET COLLCNT = {1} WHERE REVIEWID = '{0}'", reviewId, v);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateReviewStatus(string reviewId, string v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE REVIEW SET STATUS = '{1}' WHERE REVIEWID = '{0}'", reviewId, v);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateGoodsByGoodsId(string goodsId, long minLikeCnt, long minCollCnt, long minCommCnt, long maxLikeCnt, long maxCollCnt, long maxCommCnt, string memo)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE GOODS SET MINLIKECNT = {1}, MINCOLLCNT = {2}, MINCOMMCNT = {3}, MAXLIKECNT = {4}, MAXCOLLCNT = {5}, MAXCOMMCNT = {6}, MEMO = '{7}' WHERE GOODSID = '{0}'"
                , goodsId, minLikeCnt, minCollCnt, minCommCnt, maxLikeCnt, maxCollCnt, maxCommCnt, memo);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void UpdateUserStatus(string nick, string status)
        {
            Console.WriteLine("UpdateUserStatus");
            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = string.Format("UPDATE USER SET STATUS = '{1}' WHERE NICK = '{0}'", nick, status);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();

            DisconnectionToDB(dbConnection);
        }

        internal void InsertUser(User user)
        {
            Console.WriteLine("InsertUser");
            SQLiteConnection dbConnection = ConnectionToDB();
            string sql = string.Format("INSERT INTO USER (ID,  PWD, NICK, EMAIL, GENDER, YEAR, MONTH, DAY, STATUS, IMAGEYN) VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')"
            , user.id, user.pwd, user.nick, user.email, user.gender, user.year, user.month, user.day, user.status, user.imageYn);

            Console.WriteLine(sql);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
            DisconnectionToDB(dbConnection);
        }

        internal List<User> SelectAllUser()
        {
            Console.WriteLine("SelectAllUser");
            List<User> list = new List<User>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = "SELECT * FROM USER";

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                User u = new User();
                u.id = (string)reader["Id"];
                u.pwd = (string)reader["Pwd"];
                u.nick = (string)reader["Nick"];
                u.email = (string)reader["Email"];
                u.gender = (string)reader["Gender"];
                u.status = (string)reader["Status"];
                u.year = (string)reader["Year"];
                u.month = (string)reader["Month"];
                u.day = (string)reader["Day"];
                u.imageYn = (string)reader["ImageYn"];

                list.Add(u);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }

        internal int SelectUserCnt(string v)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            int rowCount = 0;
            string sql = "SELECT COUNT(*) FROM USER";

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);

            rowCount = Convert.ToInt32(command.ExecuteScalar());
            return rowCount;
        }

        internal int SelectUserCntByStatus(string status, int limit)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            int rowCount = 0;
            string sql = string.Format(  "SELECT COUNT(*) FROM USER WHERE STATUS = '{0}' LIMIT {1}", status, limit);

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);

            rowCount = Convert.ToInt32(command.ExecuteScalar());
            return rowCount;
        }

        internal bool IsExistUserNick(string nick)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            int rowCount = 0;
            string sql =  string.Format( "SELECT COUNT(*) FROM USER WHERE NICK = '{0}'" , nick);
            Console.WriteLine(sql);
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);

            rowCount = Convert.ToInt32(command.ExecuteScalar());
            return rowCount > 0 ? true : false;
        }

        internal List<Goods> SelectAllGoodsHaveStageReview()
        {
            List<Goods> list = new List<Goods>();

            SQLiteConnection dbConnection = ConnectionToDB();

            string sql = "SELECT * FROM GOODS G WHERE 1 = 1 AND EXISTS( SELECT 1 FROM REVIEW R WHERE R.GoodsId = G.GoodsId AND(R.STATUS = '[2]대기' OR R.STATUS = '[1]작업중'))";

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                Goods g = new Goods();
                g.GoodsId = (string)reader["GoodsId"];
                g.GoodsName = (string)reader["GoodsName"];
                g.MinCollCnt = (long)reader["MinCollCnt"];
                g.MinLikeCnt = (long)reader["MinLikeCnt"];
                g.MinCommCnt = (long)reader["MinCommCnt"];

                g.MaxCollCnt = (long)reader["MaxCollCnt"];
                g.MaxLikeCnt = (long)reader["MaxLikeCnt"];
                g.MaxCommCnt = (long)reader["MaxCommCnt"];

                g.Memo = (string)reader["Memo"];

                list.Add(g);
            }

            DisconnectionToDB(dbConnection);

            return list;
        }

        internal int SelectCountReviewByReviewId(String reviewId)
        {
            SQLiteConnection dbConnection = ConnectionToDB();

            int rowCount = 0;
            string sql = string.Format( "SELECT COUNT(*) FROM REVIEW R WHERE R.ReviewId = {0}", reviewId);

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);

            rowCount = Convert.ToInt32(command.ExecuteScalar());
            return rowCount;
        }
    }
}


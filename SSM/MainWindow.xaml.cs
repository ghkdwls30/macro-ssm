using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SSM;
using SSM.Model;
using SSM.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;

namespace SSM
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string STYLE_SHARE_LOGIN_URL = "https://www.styleshare.kr/login";
        private const string STYLE_SHARE_JOIN_URL = "https://www.styleshare.kr/join";
        private const string SAVE_IMAGE_PATH = @"C:\TEMP\";

        private const int DEFAULT_WAIT_SECOND = 60;
        private Thread driverWorker;
        private Thread reviewSearchWorker;
        private BackgroundWorker renderingWorker;
        private BackgroundWorker userCreateWorker;
        private List<User> userList;
        private List<string> commentList;
        private List<string> userPeedProductList;
        private SqlUtil sqlUtil;
        private List<Goods> goodsList;
        private List<Review> reviewList;
        private Dictionary<string, object> configMap;
        private Dictionary<string, string> config;
        Stopwatch stopwatch = new Stopwatch();        
        IWebDriver driver;
        IWebDriver backgroundDriver;
        private string selectGoodsId;

        public MainWindow()
        {
            InitializeComponent();
            InitBackGroundWorker();
            Init();
        }


        void RenderingWoker_DoWork(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                // 작업 버튼 종료가능         
                this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (dgGoods.SelectedItems.Count == 0)
                    {
                        goodsList = sqlUtil.SelectAllGoods();
                        UpdateDgGoods();
                    }

                    if (dgReview.SelectedItems.Count == 0 && selectGoodsId != null)
                    {
                        reviewList = sqlUtil.SelectAllReviewByGoodsId(selectGoodsId, 50);
                        UpdateDgReview();
                    }

                }));

                // 10초마다 갱신
                Thread.Sleep(1000);
            }
        }

        private void InitBackGroundWorker()
        {

            //===================================//
            // 렌더링 워커 편입 워커
            //===================================//
            renderingWorker = new BackgroundWorker();            
            renderingWorker.WorkerSupportsCancellation = true;
            renderingWorker.DoWork += new DoWorkEventHandler(RenderingWoker_DoWork);


            //===================================//
            // 아이디 생성 워커
            //===================================//
            userCreateWorker = new BackgroundWorker();
            userCreateWorker.WorkerReportsProgress = true;
            userCreateWorker.WorkerSupportsCancellation = true;
            userCreateWorker.ProgressChanged += new ProgressChangedEventHandler(UserCreateWorker_ProgressChanged);
            userCreateWorker.DoWork += new DoWorkEventHandler(UserCreateWorker_DoWork);
            userCreateWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(UserCreateWorker_RunWorkerCompleted);
        }

        // Worker Thread가 실제 하는 일
        void UserCreateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {

                // 모든 쓰레기 이미지 삭제
                DirectoryInfo di = new DirectoryInfo(SAVE_IMAGE_PATH);
                FileInfo[] files = di.GetFiles("*.jpg")
                                     .Where(p => p.Extension == ".jpg").ToArray();
                foreach (FileInfo file in files)
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                        File.Delete(file.FullName);
                    }
                    catch { }

                // 브라우저 생성
                backgroundDriver = MakeDriver(true);

                double MAX_CNT = 0;
                this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    lbProgressTxt.Content = "아이디를 수집하는 중 · · · · · ·";
                    MAX_CNT = Int32.Parse(txIdCreateMaxCnt.Text);
                }));

                double CURRENT_CNT = sqlUtil.SelectUserCntByStatus("[2]대기", (int)MAX_CNT);
                int pct = 0;
                

                foreach (string productId in userPeedProductList)
                {

                    //List<User> userList = sqlUtil.SelectAllUser();
                    // 상품페이지 이동
                    backgroundDriver.Navigate().GoToUrl(string.Format("https://www.glowpick.com/product/{0}", productId));

                    // 작업 버튼 종료가능         
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        btnIdCreateWork.IsEnabled = true;
                        btnIdCreateWork.Content = "작업 ON";
                        btnIdCreateWork.Background = Brushes.Red;
                    }));

                    ReadOnlyCollection<IWebElement> elements = backgroundDriver.FindElements(By.ClassName("review-list-item"));

                    foreach (IWebElement element in elements)
                    {
                        // 취소처리
                        if (userCreateWorker.CancellationPending)
                        {
                            ResetProgressBar();

                            e.Cancel = true;
                            return;
                        }

                        User user = new User();
                        user.id = GetRandomId() + CommonUtil.MakeRandomValue(1, 10);
                        user.nick = element.FindElement(By.CssSelector(".user-name")).Text.Replace("'", "");

                        // 이미 DB상에 있는 닉이면 패스
                        //if (userList.Where(v => v.nick.Equals(user.nick)).Select(x => x).Count() > 0) continue;
                        if (sqlUtil.IsExistUserNick(user.nick)) continue;

                        user.email = GetRandomEmail(user.id);
                        user.gender = element.FindElement(By.CssSelector(".info .txt .icon-sprite")).GetAttribute("class").Contains("icon-gender-f") ? "여자" : "남자";
                        user.pwd = MakeRandomPassword(10);
                        user.year = CommonUtil.MakeRandomValue(1999, 2008).ToString();
                        user.month = CommonUtil.MakeRandomValue(1, 12).ToString();
                        user.day = CommonUtil.MakeRandomValue(1, 28).ToString();
                        user.status = "[2]대기";

                        // 이미지 다운로드
                        string url = element.FindElement(By.CssSelector(".user-img .user-img__image")).GetCssValue("background-image");
                        url = url.Replace("url(\"", "").Replace("\")", "");
                        Console.WriteLine(url);
                        if (!url.Contains("noimage"))
                        {
                            DownloadProfileImage(url, user.id);
                            user.imageYn = "Y";
                        }
                        else
                        {
                            user.imageYn = "N";
                        }

                        // 유저 삽입
                        sqlUtil.InsertUser(user);

                        CURRENT_CNT++;

                        pct = ((int)Math.Round(((CURRENT_CNT / MAX_CNT) * 100)));
                        userCreateWorker.ReportProgress(pct);

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            lbCurrentTotalCnt.Content = MAX_CNT + " / " + CURRENT_CNT;
                        }));

                        if (CURRENT_CNT == MAX_CNT) goto EXIT;
                    }
                }

            EXIT: Console.WriteLine("반복문 탈출");

                userCreateWorker.ReportProgress(0);
                CURRENT_CNT = 0;

                this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    lbProgressTxt.Content = "아이디를 생성하는 중 · · · · · ·";
                    lbCurrentTotalCnt.Content = MAX_CNT + " / 0";
                }));

                // 대기상태인 모든 유저중 목표 카운트 만큼 가져온다.
                userList = sqlUtil.SelectUserByStatus("[2]대기", (int)MAX_CNT);

                foreach (User u in userList)
                {
                    // 취소처리
                    if (userCreateWorker.CancellationPending)
                    {
                        ResetProgressBar();
                        e.Cancel = true;

                        if (backgroundDriver != null)
                        {
                            backgroundDriver.Quit();
                            backgroundDriver = null;
                        }

                        return;
                    }

                    // 회원가입 페이지 이동
                    backgroundDriver.Navigate().GoToUrl(STYLE_SHARE_JOIN_URL);

                    // 대기
                    try
                    {
                        WaitForVisivle(backgroundDriver, By.Id("id"), 10);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }

                    // 아이디 입력            
                    IWebElement element = backgroundDriver.FindElement(By.Id("id"));
                    element.SendKeys(u.id);

                    // 패스워드 입력            
                    element = backgroundDriver.FindElement(By.Id("pwd"));
                    element.SendKeys(u.pwd);

                    // 패스워드 확인 입력            
                    element = backgroundDriver.FindElement(By.Id("pwdConfirm"));
                    element.SendKeys(u.pwd);

                    // 가입버튼 클릭
                    element = backgroundDriver.FindElement(By.ClassName("kMxQNN"));
                    element.Click();

                    // 대기
                    try
                    {
                        WaitForVisivle(backgroundDriver, By.Id("nickname"), 10);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }

                    // 닉네임
                    element = backgroundDriver.FindElement(By.Id("nickname"));
                    element.SendKeys(u.nick);

                    // 이메일
                    element = backgroundDriver.FindElement(By.Id("email"));
                    element.Clear();
                    element.SendKeys(u.email);

                    // 성별
                    if ("여자".Equals(u.gender))
                    {
                        element = backgroundDriver.FindElement(By.CssSelector("[name='gender'][value='female']"));
                        element.Click();
                    }
                    else
                    {
                        element = backgroundDriver.FindElement(By.CssSelector("[name='gender'][value='male']"));
                        element.Click();
                    }

                    // 년도
                    if (u.year.CompareTo("2006") > 0) {
                        u.year = "2006";
                    }                   
                    element = backgroundDriver.FindElement(By.CssSelector(string.Format("select.year option[value='{0}']", u.year)));
                    element.Click();

                    // 월
                    element = backgroundDriver.FindElement(By.CssSelector(string.Format("select.month option[value='{0}']", u.month)));
                    element.Click();

                    // 일
                    element = backgroundDriver.FindElement(By.CssSelector(string.Format("select.day option[value='{0}']", u.day)));
                    element.Click();

                    if ("Y".Equals(u.imageYn))
                    {
                        // 사진첨부
                        element = backgroundDriver.FindElement(By.ClassName("image-selector"));
                        element.SendKeys(SAVE_IMAGE_PATH + u.id + ".jpg");
                    }

                    // 전체동의
                    backgroundDriver.FindElement(By.CssSelector("input[name='agree-all']")).Click();                    


                    // 버튼클릭
                    element = backgroundDriver.FindElement(By.ClassName("submit-btn"));
                    element.Click();

                    try
                    {
                        WaitForVisivle(backgroundDriver, By.ClassName("profile"), 10);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }

                    backgroundDriver.Manage().Cookies.DeleteAllCookies();

                    CURRENT_CNT++;

                    pct = ((int)Math.Round(((CURRENT_CNT / MAX_CNT) * 100)));
                    userCreateWorker.ReportProgress(pct);

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        lbCurrentTotalCnt.Content = MAX_CNT + " / " + CURRENT_CNT;
                    }));

                    if (CURRENT_CNT == MAX_CNT) break;

                    sqlUtil.UpdateUserStatus(u.nick, "완료");
                }
            }
            catch(Exception ex)
            {
                Print(ex.Message);
            }
        }

        void ResetProgressBar()
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                lbProgressTxt.Content = "";
                lbCurrentTotalCnt.Content = "";
                userCreateprogressBar.Value = 0;
            }));
        }

        // Progress 리포트 - UI Thread
        void UserCreateWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            userCreateprogressBar.Value =  e.ProgressPercentage;
        }

        // 작업 완료 - UI Thread
        void UserCreateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            
            ResetProgressBar();

            if (!e.Cancelled)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    lbProgressTxt.Content = "모든 작업이 완료되었습니다.";
                }));
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    lbProgressTxt.Content = "작업이 취소 되었습니다.";
                }));
            }

            if (backgroundDriver != null)
            {
                backgroundDriver.Quit();
                backgroundDriver = null;
            }
        }

        // Worker Thread가 실제 하는 일
        void ReviewSearch_DoWork()
        {
            while (true)
            {
                stopwatch.Start();

                Print("[Review Search Worker] Start...");

                List<Goods> goodsList = sqlUtil.SelectAllGoods();

                int limitCnt = 50;

                foreach (Goods g in goodsList)
                {
                    int offset = 50;
                    int registCount = 0;

                    Print(string.Format("[INFO] GoodsId :: {0}", g.GoodsId));

                    List<Review> reviewList = sqlUtil.SelectAllReviewByGoodsId(g.GoodsId);

                    JObject o = CallGet( string.Format("https://www.styleshare.kr/goods/{0}/styles?limit={1}", g.GoodsId, limitCnt));

                    if (o != null)
                    {
                        JArray r = new JArray();
                        while (true)
                        {
                            try
                            {
                                if (o["data"] == null)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                break;
                            }
                            foreach (JObject item in (JArray)o["data"])
                            {
                                r.Add(item);
                            }
                            if (o["paging"]["next"].ToString().Equals(""))
                            {
                                break;
                            }
                            else
                            {
                                o = this.CallGet(string.Format("https://www.styleshare.kr/goods/{0}/styles?limit={1}&offset={2}", g.GoodsId, limitCnt, offset));
                                if (o != null)
                                {
                                    offset += 50;
                                    this.Print(string.Format("[INFO] Review Count : {0}", r.Count));
                                }
                                else
                                {
                                    Thread.Sleep(10000);
                                }
                            }
                        }

                        var distinicJarray = r.GroupBy(e => e["id"].ToString()).Select(e => e.First());

                        foreach (JObject item in distinicJarray)
                        {
                            DateTime createdAt = (DateTime)item["createdAt"];
                            DateTime now = DateTime.Now;
                            TimeSpan diff = (now - createdAt);

                            // 이미 해당된 리뷰라면 건너뜀
                            if (sqlUtil.SelectCountReviewByReviewId(item["id"].ToString()) > 0) continue;
                            //if (reviewList.Where(x => x.ReviewId.Equals(item["id"].ToString())).Select(x => x).Count() > 0) continue;

                            // 최근 시간에 해당되지 않는다면 건너 뜀
                            int reviewProid = 86400;
                            this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                            {
                                reviewProid = Convert.ToInt32(txReviewPriod.Text);
                            }));

                            if (diff.TotalSeconds > reviewProid) continue;

                            Review review = new Review();
                            review.GoodsId = g.GoodsId;
                            review.ReviewId = item["id"].ToString();
                            review.ReviewName = ((string)item["description"]).Replace("\n", " ").Replace("\r\n", " ").Replace("\r", " ");
                            review.LikeCnt = 0;
                            review.CommCnt = 0;
                            review.CollCnt = 0;
                            review.TotalCollCnt = CommonUtil.MakeRandomValue((int)g.MinCollCnt, (int)g.MaxCollCnt);
                            review.TotalCommCnt = CommonUtil.MakeRandomValue((int)g.MinCommCnt, (int)g.MaxCommCnt);
                            review.TotalLikeCnt = CommonUtil.MakeRandomValue((int)g.MinLikeCnt, (int)g.MaxLikeCnt);
                            review.RegDt = createdAt.ToString("yyyy/MM/dd HH:mm:ss");
                            review.Status = "[2]대기";

                            sqlUtil.InsertReview(review);

                            registCount++;
                        }

                        Print(string.Format("[INFO] Review Total Count : {0}", r.Count));
                        Print(string.Format("[INFO] Review Regist Count : {0}", registCount));

                    }
                    else
                    {

                        Thread.Sleep(10000);
                    }
                }

                stopwatch.Stop();
                Print("[Review Search Worker] End...");
                Print(string.Format("Elapsed Time : {0}ms", stopwatch.ElapsedMilliseconds.ToString()));

                stopwatch.Reset();

                int minReviewSearch = 1;
                int maxReviewSearch = 1;

                this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    // 검색주기 랜덤 값
                    minReviewSearch = Convert.ToInt32(txMinReviewSearch.Text);
                    maxReviewSearch = Convert.ToInt32(txMaxReviewSearch.Text);
                }));

                Thread.Sleep(CommonUtil.MakeRandomValue(minReviewSearch * 1000, maxReviewSearch * 1000));

            }
        }

       // Worker Thread가 실제 하는 일
       void Driver_DoWork()
        {
            // 브라우저 생성
            driver = MakeDriver();

            while (true)
            {
                int min = 0;
                int max = 0;

                List<Goods> goodsList = sqlUtil.SelectAllGoodsHaveStageReview();

                foreach (Goods g in goodsList)
                {
                    // 상품에 대상 리뷰가 존재 할 경우에만 이후 프로세스 진행
                    List<Review> reviewList = sqlUtil.SelectStageReviewByGoodsId(g.GoodsId);
                    if (reviewList == null || reviewList.Count == 0)
                    {
                        Print(string.Format("[INFO] Not Found Abalable Review!"));
                        WaitSecond(60, 60);
                        continue;
                    }
                    

                    // 리스팅 갱신을 위한 세팅
                    selectGoodsId = g.GoodsId;

                    Print(string.Format("[Process Worker] Start"));
                    Print(string.Format("[INFO] GoodsId :: {0}", g.GoodsId));

                    // 작업중으로 된것 다 초기화 시켜서 대기로 변경
                    sqlUtil.UpdateAllReviewStatusResetByGoodsId(g.GoodsId);
                 

                    // 상품당 최대 처리량 
                    this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        min = Convert.ToInt32(txMinCycleReviewWorkCnt.Text);
                        max = Convert.ToInt32(txMaxCycleReviewWorkCnt.Text);
                    }));

                    int maxReviewWorkCnt = CommonUtil.MakeRandomValue( min, max);
                    int currentReviewWorkCnt = 0;

                    while (true)
                    { 
                        reviewList = sqlUtil.SelectStageReviewByGoodsId(g.GoodsId);

                        Print(string.Format("[INFO] Total Review Count :: {0}", reviewList.Count));

                        if (reviewList == null || reviewList.Count == 0) break;

                        // 리뷰 랜덤 소팅
                        ShuffleMe(reviewList);
                        //reviewList = reviewList.OrderBy(x => new Random().Next()).ToList();

                        foreach (Review r in reviewList)
                        {
                            // 픽넘버에 따른 댓글, 담아요, 좋아요 중 한개를 선택해서 작업 침;
                            // 좋아요 : 1, 담아요 : 2, 댓글 : 3

                            //Print(string.Format("START >>> 리뷰"));

                            //Print(string.Format("[INFO] 리뷰ID : {0}", r.ReviewId));
                            Print(string.Format("[INFO] ReviewID :: {0}", r.ReviewId));

                            List<int> excludePickList = new List<int>();

                            if (r.TotalLikeCnt == r.LikeCnt) excludePickList.Add(1);
                            if (r.TotalCollCnt == r.CollCnt) excludePickList.Add(2);
                            if (r.TotalCommCnt == r.CommCnt) excludePickList.Add(3);

                            int pickNumber = CommonUtil.MakeRandomValue(1, 3, excludePickList);
                            //pickNumber = 3;


                            if (pickNumber == 1)
                            {
                                // 좋아요
                                Print(string.Format("[INFO] WorkType : {0}", "Like"));
                            }
                            else if (pickNumber == 2) {
                                // 담아요
                                Print(string.Format("[INFO] WorkType : {0}", "Collect"));
                            }
                            if (pickNumber == 3) {
                                // 댓글
                                Print(string.Format("[INFO] WorkType : {0}", "Comment"));
                            }

                            
                            //pickNumber = 3;
                            sqlUtil.UpdateReviewStatus(r.ReviewId, "[1]작업중");

                            LOGIN :

                            // 랜덤 유저
                            User user = GetRandomUser();

                            Print(string.Format("[INFO] UserId : {0}", user.id));

                            // 로그인
                            bool isLogin = DoLogin(driver, user.id, user.pwd);

                            if (!isLogin)
                            {
                                goto LOGIN;
                                //sqlUtil.UpdateReviewStatus(r.ReviewId, "대기");
                                //continue;
                            }

                            // 작업 버튼 종료가능         
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            {
                                btnWork.IsEnabled = true;
                                btnWork.Content = "작업 ON";
                                btnWork.Background = Brushes.Red;
                            }));

                            Print(string.Format("[INFO] Move Goods Page"));
                            //Print(string.Format("START >>> 상품 페이지 이동"));

                            // 상품 페이지 이동
                            driver.Navigate().GoToUrl( string.Format( "https://www.styleshare.kr/goods/{0}", g.GoodsId));

                            //Print(string.Format("END >>> 상품 페이지 이동"));

                            // 상품명 업데이트
                            if (g.GoodsName == null || g.GoodsName.Trim().Length == 0)
                            {
                                ReadOnlyCollection<IWebElement> e = driver.FindElements(By.XPath("//*[@id='app']/div/div[1]/div[1]/p[2]"));
                                sqlUtil.UpdateGoodsNameByGoodsId(g.GoodsId, e[0].Text);
                            }

                            Print(string.Format("[INFO] Move Review Page"));

                            //Print(string.Format("START >>> 후기이동"));

                            // 후기 이동
                            MoveReview(driver, r.ReviewId);

                            WaitSecond(1);

                            // 삭제된 리뷰인지 판단.
                            //IWebElement e = driver.FindElement(By.CssSelector(".error-code"));

                            try
                            {
                                Print(string.Format("[INFO] WaitForVisible Review Page : {0}", r.ReviewId));
                                Print(string.Format("[INFO] Wait 0(Min) - 60(Max) Second"));
                                WaitForVisivle(this.driver, By.CssSelector(".side-position .information"), 60);
                            }
                            catch (Exception e)
                            {
                                this.sqlUtil.UpdateReviewStatus(r.ReviewId, "[4]삭제");
                                this.driver.Manage().Cookies.DeleteAllCookies();
                                continue;
                            }

                            //Print(string.Format("END >>> 후기이동"));

                            if (r.TotalLikeCnt > r.LikeCnt && pickNumber == 1)
                            {
                                Print(string.Format("[INFO] Like Working"));
                                //Print(string.Format("START >>> 좋아요"));

                                // 좋아요 
                                bool result = DoLike(driver);

                                if (result)
                                {
                                    sqlUtil.UpdateReviewLikeCnt(r.ReviewId, ++r.LikeCnt);
                                }

                                WaitSecond(1);

                                //Print(string.Format("END >>> 좋아요"));
                            }

                            if (r.TotalCollCnt > r.CollCnt && pickNumber == 2)
                            {
                                //Print(string.Format("START >>> 담아요"));
                                Print(string.Format("[INFO] Collect Working"));

                                // 담아요
                                bool result = DoCollection(driver);
                                if (result)
                                {
                                    sqlUtil.UpdateReviewCollCnt(r.ReviewId, ++r.CollCnt);
                                }
                            
                                WaitSecond(1);

                                //Print(string.Format("END >>> 담아요"));
                            }

                            if (r.TotalCommCnt > r.CommCnt && pickNumber == 3)
                            {
                                // 랜덤 댓글
                                string comment = GetRandomComment();

                                //Print(string.Format("START >>> 댓글"));
                                Print(string.Format("[INFO] Comment Working"));

                                // 댓글 달기
                                DoComment(driver, comment);
                                sqlUtil.UpdateReviewCommCnt(r.ReviewId, ++r.CommCnt);
                                WaitSecond(1);

                                //Print(string.Format("END >>> 댓글"));
                            }

                            //Print(string.Format("START >>> 로그아웃"));
                            Print(string.Format("[INFO] Logout Working"));
                            DoLogout(driver);
                            //Print(string.Format("END >>> 로그아웃"));

                            //WaitForVisivle(driver, By.ClassName("login-join"), DEFAULT_WAIT_SECOND);

                            if (r.TotalCollCnt == r.CollCnt && r.TotalLikeCnt == r.LikeCnt && r.TotalCommCnt == r.CommCnt)
                            {
                                sqlUtil.UpdateReviewStatus(r.ReviewId, "[3]완료");
                            }
                            else
                            {
                                sqlUtil.UpdateReviewStatus(r.ReviewId, "[2]대기");
                            }

                            // 작업 버튼 종료가능         
                            this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                            {
                                min = Convert.ToInt32(txMinEachReviewWaitTime.Text);
                                max = Convert.ToInt32(txMaxEachReviewWaitTime.Text);
                            }));

                            Print(string.Format("[INFO] Done Review Working"));

                            // 최대 작업 처리량
                            currentReviewWorkCnt++;
                            if (currentReviewWorkCnt == maxReviewWorkCnt || currentReviewWorkCnt == reviewList.Count) {
                                goto EXIT_REVIEW;
                            }

                            // 각 리뷰별 대기시간
                            WaitSecond(min, max);
                        }
                    }

                    EXIT_REVIEW: Console.WriteLine("[INFO] EXIT_REVIEW");

                    // 상품간 대기시간       
                    this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        min = Convert.ToInt32(txMinCycleReviewWaitTime.Text);
                        max = Convert.ToInt32(txMaxCycleReviewWaitTime.Text);
                    }));


                    Print(string.Format("[Process Worker] End"));

                    // 각 상품별 대기시간
                    WaitSecond(min, max);
                }

                this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    min = Convert.ToInt32(txMinCycleWaitTime.Text);
                    max = Convert.ToInt32(txMaxCycleWaitTime.Text);
                }));

                // 사이클 간 대기시간
                WaitSecond(min, max);
            }

            // 작업 버튼 종료가능         
            /*this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                STOP();
                btnWork.IsEnabled = true;
            }));*/
        }

        public void ShuffleMe<T>(IList<T> list)
        {
            Random random = new Random();
            int n = list.Count;

            for (int i = list.Count - 1; i > 1; i--)
            {
                int rnd = random.Next(i + 1);

                T value = list[rnd];
                list[rnd] = list[i];
                list[i] = value;
            }            
        }

        private bool IsElementPresent(By by)
        {
            try
            {
                driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        private string GetRandomComment()
        {
            int v = CommonUtil.MakeRandomValue(0, commentList.Count - 1);
            return commentList[v];
        }

        private User GetRandomUser()
        {
            int v =CommonUtil.MakeRandomValue(0, userList.Count - 1);
            return userList[v];
        }

        private void DoLogout(IWebDriver driver)
        {
            /* IWebElement e = driver.FindElement(By.CssSelector(".user-profile-button"));
             e.Click();

             WaitSecond(1);

             e = driver.FindElement(By.CssSelector(".menu-list .ic_logout"));
             e.Click();*/

            driver.Manage().Cookies.DeleteAllCookies();

            //Thread.Sleep(2000);
        }

        private void DoComment(IWebDriver driver, string v)
        {


            // SSM 댓글 어뷰징 방지로 인해 2가지 텍스트에어리어로 가상으로 입력받음 씹새끼들 그래서 아래처럼 코딩함
            IWebElement e = null;

            try
            {
                e = driver.FindElement(By.XPath("//*[@id='int7-cg']/div[3]/label/div/textarea[1]"));
                e.Click();
            }
            catch (Exception ex) {
                e = driver.FindElement(By.XPath("//*[@id='int7-var1']/div[3]/label/div/textarea[1]"));
                e.Click();
            }
            
            //*[@id="int7-cg"]/div[3]/label/div/textarea[1]

            //IWebElement e = driver.FindElement(By.CssSelector(".comment-wrapper.op-comment-component textarea.input.op-input"));
            //IWebElement e = driver.FindElement(By.XPath("//*[@id='int7-var1']/div[3]/label/div/textarea[1]"));
            //e.SendKeys(Keys.Enter);



            //IJavaScriptExecutor js = (IJavaScriptExecutor)driver;



            //js.ExecuteAsyncScript("arguments[0].click();", e);

            WaitSecond(1);

            e.SendKeys(v);
            e.SendKeys(Keys.Return);
        }

        private bool DoCollection(IWebDriver driver)
        {
            bool result = false;

            // 일단 담아요 클릭
            IWebElement e = driver.FindElement(By.CssSelector(".collect-action"));
            e.Click();

            // 콜렉션이 보일 때 까지 대기
            WaitForVisivle(driver, By.ClassName("collections"), DEFAULT_WAIT_SECOND);
            
            // 좋아요 여부 확인
            e = driver.FindElement(By.CssSelector(".my-collection .collections .collection:first-child .custom-checkbox"));

            // 담아요를 하지 않았다면
            if (!HasClass(e, "checked"))
            {

                // 체크박스 클릭
                e.FindElement(By.ClassName("op-checkbox")).Click();

                WaitSecond(1);

                // 확인 버튼 클릭
                e = driver.FindElement(By.CssSelector(".modal.displayed .modal-lower-body .op-ok-btn"));
                e.Click();

                result = true;
            }
            else
            {
                // 취소 버튼 클릭
                e = driver.FindElement(By.CssSelector(".modal.displayed .modal-lower-body .op-cancel-btn"));
                e.Click();
            }

            return result;
        }

        private void Print(object v) {
            Console.WriteLine(v);
        }

        public bool HasClass( IWebElement el, string className)
        {
            return el.GetAttribute("class").Split(' ').Contains(className);
        }

        private bool DoLike(IWebDriver driver)
        {
            bool result = false; 
            // 좋아요 여부 확인
            IWebElement e = driver.FindElement(By.CssSelector(".like-action"));

            // 좋아요를 하지 않았다면
            if(HasClass(e, "not-like"))
            {
                e.FindElement(By.CssSelector(".like-btn")).Click();
                result = true;
            }

            return result;
            
            //IWebElement e = driver.FindElement(By.ClassName("like-btn"));
            //e.SendKeys(id);
        }

        // 로그인 처리
        private bool DoLogin(IWebDriver driver, string id, string pwd)
        {
            bool isLogin = true;

            // 로그인 페이지 이동
            driver.Navigate().GoToUrl(STYLE_SHARE_LOGIN_URL);
         
            // 로그인 버튼이 보일 때 까지 대기
            WaitForVisivle(driver, By.CssSelector("button[type=submit]"), DEFAULT_WAIT_SECOND);

            // 아이디 입력            
            IWebElement e = driver.FindElement(By.Id("id"));
            e.SendKeys(id);

            WaitSecond(1);

            // 패스워드 입력
            e = driver.FindElement(By.Id("pwd"));
            e.SendKeys(pwd);

            WaitSecond(1);

            // 로그인 버튼 클릭
            e = driver.FindElement(By.CssSelector("button[type=submit]"));
            e.Click();

            try
            {
                WaitForVisivle(driver, By.CssSelector("li[value=profile]"), 10);
            }
            catch (Exception ex)
            {
                isLogin = false;
            }

            return isLogin;
        }

        private void WaitSecond(int v)
        {
            Thread.Sleep(v * 1000);
        }

        private void WaitSecond(int min, int max)
        {
            int v = CommonUtil.MakeRandomValue(min, max);


            Print(string.Format("[INFO] Wait :: {0} ", v));
            Thread.Sleep(v * 1000);
        }

        private void MoveReview(IWebDriver driver, string v)
        {            
            driver.Navigate().GoToUrl("https://www.styleshare.kr/inum/" + v);
        }

        void Driver_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
           // 작업완료
        }

        private void Init()
        {
            //===================================//
            // 데이터베이스 연결
            //===================================//
            sqlUtil = new SqlUtil();

            //===================================//
            // 설정 정보 로드
            //===================================//
            LoadConfig();

            //===================================//
            // 컨피그 설정
            //===================================//
            config = new Dictionary<string, string>();
            config.Add(ConfigKey.CONFIG_REVIEW_PRIOD, "txReviewPriod");
            config.Add(ConfigKey.CONFIG_MIN_REVIEW_SEARCH_TIME, "txMinReviewSearch");
            config.Add(ConfigKey.CONFIG_MAX_REVIEW_SEARCH_TIME, "txMaxReviewSearch");
            config.Add(ConfigKey.CONFIG_MIN_EACH_REVIEW_WAIT_TIME, "txMinEachReviewWaitTime");
            config.Add(ConfigKey.CONFIG_MAX_EACH_REVIEW_WAIT_TIME, "txMaxEachReviewWaitTime");
            config.Add(ConfigKey.CONFIG_MIN_CYCLE_REVIEW_WAIT_TIME, "txMinCycleReviewWaitTime");
            config.Add(ConfigKey.CONFIG_MAX_CYCLE_REVIEW_WAIT_TIME, "txMaxCycleReviewWaitTime");
            config.Add(ConfigKey.CONFIG_MIN_CYCLE_WAIT_TIME, "txMinCycleWaitTime");
            config.Add(ConfigKey.CONFIG_MAX_CYCLE_WAIT_TIME, "txMaxCycleWaitTime");
            config.Add(ConfigKey.CONFIG_MIN_CYCLE_REVIEW_WORK_COUNT, "txMinCycleReviewWorkCnt");
            config.Add(ConfigKey.CONFIG_MAX_CYCLE_REVIEW_WORK_COUNT, "txMaxCycleReviewWorkCnt");

            //===================================//
            // 컴포넌트 로드
            //===================================//
            LoadComponent();

            //===================================//
            // 렌더링 워커 실행
            //===================================//
            renderingWorker.RunWorkerAsync();
        }

        private void LoadComponent()
        {
            //===================================//
            // 대시보드 초기화
            //===================================//

            // 1. 상품 리스트 초기화
            goodsList = sqlUtil.SelectAllGoods();            
            dgGoods.ItemsSource = goodsList;

            //===================================//
            // 설정 탭 초기화
            //===================================//

            foreach (KeyValuePair<string, string> c in config)
            {
                if (configMap.ContainsKey(c.Key))
                {
                    var textBox = (TextBox)this.FindName(c.Value);
                    textBox.Text = configMap[c.Key].ToString();
                }
            }
        }

        private void LoadConfig()
        {
            // 유저정보 읽기
            userList = ReadUserListFromCSV("./Config/User_List.csv");

            // 아이디생성 유저피드 상품
            userPeedProductList = ReadStringFromCSV("./Config/UserId_Peed_Product.csv");

            // 코맨트 리스트 읽기
            commentList = ReadStringFromCSV("./Config/Comment_List.csv");

            // 설정정보 읽기 
            configMap = sqlUtil.SelectAllConfigMap();
        }

        private List<string> ReadStringFromCSV(string filePath)
        {
            List<string> l = new List<string>();

            using (TextFieldParser parser = new TextFieldParser(filePath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    string[] col = parser.ReadFields();
                    l.Add(col[0]);
                }
            }

            return l;
        }

        private List<User> ReadUserListFromCSV(string filePath)
        {
            List<User> l = new List<User>();

            using (TextFieldParser parser = new TextFieldParser(filePath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    string[] col = parser.ReadFields();

                    User user = new User();
                    user.id = col[0];
                    user.pwd = col[1];

                    l.Add(user);
                }
            }

            return l;
        }

        private static void WaitForVisivle(IWebDriver driver, By by, int seconds)
        {
            
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(seconds));
            wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));// instead of id u can use cssSelector or xpath of ur element.
        }
        private IWebDriver MakeDriver()
        {
            return MakeDriver(false);
        }
        private IWebDriver MakeDriver(bool isHide)
        {
            ChromeOptions cOptions = new ChromeOptions();
            cOptions.AddArguments("disable-infobars");
            cOptions.AddArguments("--js-flags=--expose-gc");
            cOptions.AddArguments("--enable-precise-memory-info");
            cOptions.AddArguments("--disable-popup-blocking");
            cOptions.AddArguments("--disable-default-apps");
            cOptions.AddArguments("--window-size=1280,1080");
            cOptions.AddArguments("--incognito");

            if (false)
            {
                cOptions.AddArguments("headless");
            }

            ChromeDriverService chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.HideCommandPromptWindow = true;


            // 셀레니움실행
            IWebDriver driver = new ChromeDriver(chromeDriverService, cOptions);
            //driver.Manage().Window.Maximize();
            //driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(60);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
            
            return driver;
        }

        // 스타일 쉐어 이동
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //driverWorker.RunWorkerAsync();
        }

        // 최근 리뷰 조회
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
           JObject o  =  CallGet("https://www.styleshare.kr/goods/213257/styles?limit=4");
        }

        // GET 요청
        private JObject CallGet(string url)
        {
            JObject jObjects;
            string responseText = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 30000;
            request.ContentType = "application/json";
            request.Accept = "application/json";
            responseText = string.Empty;
            try
            {
                using (WebResponse resp = request.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        jObjects = JObject.Parse(sr.ReadToEnd());
                    }
                }
            }
            catch (Exception e)
            {
                jObjects = null;
            }
            return jObjects;
        }

        // GET 요청
        private XmlDocument CallGetXML(string url)
        {
            string responseText = string.Empty;            

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "text/xml";        
            request.Method = "GET";
            request.Timeout = 30 * 1000;

            // Response 처리
            responseText = string.Empty;
            using (WebResponse resp = request.GetResponse())
            {
                Stream respStream = resp.GetResponseStream();
                using (StreamReader sr = new StreamReader(respStream))
                {
                    responseText = sr.ReadToEnd();
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(responseText);
                    return xml;
                }
            }
        }

        // 상품추가
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (txGoodsId.Text.Trim().Length == 0)
            {
                MessageBoxEx.Show(this,"상품번호를 입력하세요");
                return;
            }

            Goods goods = new Goods { GoodsId = txGoodsId.Text, GoodsName = "", MinLikeCnt = 1, MaxLikeCnt=1, MinCollCnt = 1, MaxCollCnt =1, MinCommCnt = 1, MaxCommCnt=1 ,Memo = "" };
            sqlUtil.InsertGoods(goods);
            goodsList.Add(goods);
            UpdateDgGoods();
        }

        // 상품 선택 삭제
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            IList selectedRows = dgGoods.SelectedItems;
            var selectedGoodsList = selectedRows.Cast<Goods>();

            foreach (Goods g in selectedGoodsList)
            {
                sqlUtil.DeleteGoodsByGoodsId(g.GoodsId);
                goodsList = goodsList.Where(item => item.GoodsId != g.GoodsId).ToList();
            }

            UpdateDgGoods();

            if (selectGoodsId != null)
            {
                reviewList = sqlUtil.SelectAllReviewByGoodsId(selectGoodsId, 50);
                UpdateDgReview();
            }
        }

        private void UpdateDgGoods()
        {
            dgGoods.ItemsSource = goodsList;
            dgGoods.Items.Refresh();            
        }

        private void UpdateDgReview()
        {
            dgReview.ItemsSource = reviewList;
            dgReview.Items.Refresh();
        }

        private void DgGoods_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ensure row was clicked and not empty space
            DataGridRow row = ItemsControl.ContainerFromElement((DataGrid)sender, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row == null) return;

            // 상품아이디
            Goods g = (Goods)row.Item;
            selectGoodsId = g.GoodsId;

            // 리뷰 데이터 갱신
            reviewList = sqlUtil.SelectAllReviewByGoodsId(g.GoodsId, 50);            
            UpdateDgReview();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 모든 설정 저장
            foreach (KeyValuePair<string, string> c in config)
            {
                var textBox = (TextBox)this.FindName(c.Value);                    
                sqlUtil.InsertOrUpateConfigByKey(c.Key, textBox.Text);
            }
        }

        // 리뷰 편입 실행
        private void BtnReviewWork_Click(object sender, RoutedEventArgs e)
        {

           Button btn  = ((Button)sender);
            if (btn.Content.ToString().Contains("OFF"))  // 시작
            {
                btn.Content = "리뷰 편입 ON";
                btn.Background = Brushes.Red;

                reviewSearchWorker = new Thread(new ThreadStart(ReviewSearch_DoWork));
                reviewSearchWorker.Start();

            }
            else // 종료
            {
                btn.Content = "리뷰 편입 OFF";
                btn.Background = Brushes.Black;

                if (reviewSearchWorker != null)
                {
                    reviewSearchWorker.Abort();
                    reviewSearchWorker = null;
                }
            }
        }
        
        // 작업 실행
        private void BtnWork_Click(object sender, RoutedEventArgs e)
        {
            Button btn = ((Button)sender);
            if (btn.Content.ToString().Contains("OFF"))  // 시작
            {
                START();
            }
            else // 종료
            {
                STOP();
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tabItem = ((sender as TabControl).SelectedItem as TabItem).Header as string;

            switch (tabItem)
            {
                case "아이디생성":
                    {
                        int cnt = sqlUtil.SelectUserCnt("완료");
                        lbAbleUserCnt.Content = string.Format("{0:#,##0}", cnt + "개의 계정이 사용 가능합니다");

                    } break;

                case "Item2":
                    break;

                case "Item3":
                    break;

                default:
                    return;
            }
        }

        private void START()
        {
            btnWork.Content = "작업 PENDING";
            btnWork.Background = Brushes.Gainsboro;
            btnWork.IsEnabled = false;

            //btnAdd.IsEnabled = false;
            //btnAdd.Background = Brushes.Gainsboro;

            //btnDelete.IsEnabled = false;
            //btnDelete.Background = Brushes.Gainsboro;

            //dgGoods.IsEnabled = false;

            driverWorker = new Thread(new ThreadStart(Driver_DoWork));
            driverWorker.Start();
        }

        private void STOP()
        {
            btnWork.Content = "작업 OFF";
            btnWork.Background = Brushes.Black;

            //btnAdd.IsEnabled = true;
            //btnAdd.Background = Brushes.Black;

            //btnDelete.IsEnabled = true;
            //btnDelete.Background = Brushes.Black;

            dgGoods.IsEnabled = true;

            if (driverWorker != null)
            {
                driverWorker.Abort();
                driverWorker = null;
            }

            if (driver != null)
            {
                driver.Quit();
                driver = null;
            }
        }

        private void DgGoods_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var column = e.Column as DataGridBoundColumn;
                if (column != null)
                {
                    var bindingPath = (column.Binding as Binding).Path.Path;
                    if (bindingPath == "MinLikeCnt" || bindingPath == "MaxLikeCnt" || bindingPath == "MinCollCnt" ||
                        bindingPath == "MaxCollCnt" || bindingPath == "MinCommCnt" || bindingPath == "MaxCommCnt" || bindingPath == "Memo")
                    {

                        TextBox t = e.EditingElement as TextBox;
                        string editedCellValue = t.Text.ToString();

                        DataGridRow row = e.Row;
                        var g = row.Item as Goods;

                        if (bindingPath == "MinLikeCnt")
                        {
                            g.MinLikeCnt = long.Parse( editedCellValue);
                        }
                        if (bindingPath == "MaxLikeCnt")
                        {
                            g.MaxLikeCnt = long.Parse(editedCellValue);
                        }
                        if (bindingPath == "MinCollCnt")
                        {
                            g.MinCollCnt = long.Parse(editedCellValue);
                        }
                        if (bindingPath == "MaxCollCnt")
                        {
                            g.MaxCollCnt = long.Parse(editedCellValue);
                        }
                        if (bindingPath == "MinCommCnt")
                        {
                            g.MinCommCnt = long.Parse(editedCellValue);
                        }
                        if (bindingPath == "MaxCommCnt")
                        {
                            g.MaxCommCnt = long.Parse(editedCellValue);
                        }
                        if (bindingPath == "Memo")
                        {
                            g.Memo = editedCellValue;
                        }

                        sqlUtil.UpdateGoodsByGoodsId(g.GoodsId, g.MinLikeCnt, g.MinCollCnt, g.MinCommCnt, g.MaxLikeCnt, g.MaxCollCnt, g.MaxCommCnt, g.Memo);
                    }
                }
            }
        }

        // 글로우픽에서 아이디 탐색
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            // 브라우저 생성
            driver = MakeDriver();

            int MAX_CNT = 100;
            int CURRENT_CNT = 0;

            

            foreach (string productId in userPeedProductList)
            {
                // 상품페이지 이동
                driver.Navigate().GoToUrl(string.Format("https://www.glowpick.com/product/{0}", productId));

                ReadOnlyCollection<IWebElement> elements = driver.FindElements(By.ClassName("review-list-item"));

                //List<User> userList = sqlUtil.SelectAllUser();

                foreach (IWebElement element in elements)
                {
                    User user = new User();
                    user.id = GetRandomId();
                    user.nick = element.FindElement(By.CssSelector(".user-name")).Text;

                    // 이미 DB상에 있는 닉이면 패스
                    //if (userList.Where(v => v.nick.Equals(user.nick)).Select(x => x).Count() > 0) continue;
                    if (sqlUtil.IsExistUserNick(user.nick)) continue;

                    user.email = GetRandomEmail(user.id);
                    user.gender = element.FindElement(By.CssSelector(".info .txt .icon-sprite")).GetAttribute("class").Contains("icon-gender-f") ? "여자" : "남자";
                    user.pwd = MakeRandomPassword(10);
                    user.year = CommonUtil.MakeRandomValue(1999, 2008).ToString();
                    user.month = CommonUtil.MakeRandomValue(1, 12).ToString();
                    user.day = CommonUtil.MakeRandomValue(1, 28).ToString();
                    user.status = "[2]대기";

                    
                    // 이미지 다운로드
                    string url = element.FindElement(By.CssSelector(".user-img .user-img__image")).GetCssValue("background-image");
                    url = url.Replace("url(\"", "").Replace("\")", "");
                    Console.WriteLine(url);
                    if (!url.Contains("noimage"))
                    {
                        DownloadProfileImage(url, user.id);
                        user.imageYn = "Y";
                    }
                    else
                    {
                        user.imageYn = "N";
                    }

                    // 유저 삽입
                    sqlUtil.InsertUser(user);
                    

                    CURRENT_CNT++;

                    if (CURRENT_CNT == MAX_CNT) goto EXIT;
                }
            }

            EXIT : Console.WriteLine("반복문 탈출");
        }
        
        private void DownloadProfileImage(string url, string name)
        {
            using (WebClient client = new WebClient())
            {
                //client.DownloadFile(new Uri(url), @"c:\temp\image35.png");
                client.DownloadFileAsync(new Uri(url), SAVE_IMAGE_PATH + name + ".jpg");
            }
        }

        public void SaveImage(string filename, string url, ImageFormat format)
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead(url);
            System.Drawing.Bitmap bitmap; bitmap = new System.Drawing.Bitmap(stream);

            if (bitmap != null)
                bitmap.Save(filename, ImageFormat.Jpeg);

            stream.Flush();
            stream.Close();
            client.Dispose();
        }

        public string MakeRandomPassword(int length)
        {
            string allowed = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            return new string(allowed
                .OrderBy(o => Guid.NewGuid())
                .Take(length)
                .ToArray());
        }

        private string GetRandomEmail(string id)
        {
            /*    string[] emailList = {
                    "outlook.com"
                    , "yahoo.com"
                    , "aol.com"
                    , "nate.com"
                    , "google.com"
                    , "naver.com"
                    , "daum.net"
                    , "daum.net"
                };
                return id + "@" + emailList[CommonUtil.MakeRandomValue(0, emailList.Length - 1)];*/
            return id + "@ruu.kr";
        }

        private void Scroll(int c)
        {
            // 스크롤 다운icon-sprite icon-gender-f
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            for (int i = 0; i < c; i++)
            {
                js.ExecuteScript("document.querySelector('#gp-default').scrollTop = document.querySelector('#gp-default').scrollHeight - 100;");
                Thread.Sleep(3000);
            }
        }

        // XML 파싱
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 100; i++)
            {
                Print(GetRandomId());
            }
        }

        private string GetRandomId()
        {
            string result = "";

            while (true)
            { 
                XmlDocument xml = CallGetXML("https://en.wikipedia.org/w/api.php?action=query&format=xml&list=random&rnnamespace=0");
                XmlNode n = xml.SelectSingleNode("/api/query/random/page");
                string title = n.Attributes["title"].Value;
                title = title.Replace("(", "")
                             .Replace("-", "")
                             .Replace(",", "")
                             .Replace("'", "")
                             .Replace("`", "")
                             .Replace("\"", "")
                             .Replace(".", "")
                             .Replace(":", "")
                             .Replace("!", "")
                             .Replace("@", "")
                             .Replace("#", "")
                             .Replace("$", "")
                             .Replace("%", "")
                             .Replace("^", "")
                             .Replace("&", "")
                             .Replace("*", "")                             
                             .Replace(")", "");
            
                string[] ids = title.Split(' ');
                result = ids[ CommonUtil.MakeRandomValue(0, ids.Length - 1)].ToLower();

                if (result.Length < 3 || isNumeric(result.Substring(0, 1))) continue;

                break;
            }
            
            return result;
        }

        private bool isNumeric(string v)
        {
            int n;
            bool isNumeric = int.TryParse(v, out n);
            return isNumeric;
        }

        // 스타일쉐어 아이디 생성
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {

        }

        // 아이디 생성작업
        private void BtnCSVExport_Click(object sender, RoutedEventArgs e)
        {
            if (txIdCreateMaxCnt.Text.Trim().Length == 0)
            {
                MessageBoxEx.Show(this, "목표개수를 입력하세요");
                return;
            }

            Button btn = ((Button)sender);
            if (btn.Content.ToString().Contains("OFF"))  // 시작
            {
                UserCreateSTART();
            }
            else // 종료
            {
                UserCreateSTOP();
            }
        }

        private void UserCreateSTART()
        {
            btnIdCreateWork.Content = "작업 PENDING";
            btnIdCreateWork.Background = Brushes.Gainsboro;
            btnIdCreateWork.IsEnabled = false;

            txIdCreateMaxCnt.IsEnabled = false;

            userCreateWorker.RunWorkerAsync();
        }

        private void UserCreateSTOP()
        {
            btnIdCreateWork.Content = "작업 OFF";
            btnIdCreateWork.Background = Brushes.Black;

            txIdCreateMaxCnt.IsEnabled = true;

            userCreateWorker.CancelAsync();
        }

        // CSV 배포
        private void BtnIdCreateWork_Click(object sender, RoutedEventArgs e)
        {

            if (txDeployCnt.Text.Trim().Length == 0)
            {
                MessageBoxEx.Show(this, "배포개수를 입력하세요");
                return;
            }

            //before your loop
            var csv = new StringBuilder();

            //enhance,aasd147@,X,아스터,ajjdf37 @auu.kr,여자,1993,8,12
            userList = sqlUtil.SelectUserByStatus("완료", Int32.Parse(txDeployCnt.Text));

            foreach (User u in userList)
            {
                //Suggestion made by KyleMit
                var newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                    u.id, u.pwd, "O", u.nick,u.email,u.gender,u.year,u.month,u.day);
                csv.AppendLine(newLine);
            }

            //after your loop
            File.WriteAllText("./Config/User_List.csv", csv.ToString());

            MessageBoxEx.Show(this, "배포가 완료 되었습니다. 프로그램을 재실행 해주세요.");
        }
    }
}


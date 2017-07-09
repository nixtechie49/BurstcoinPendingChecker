using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Web;
using System.Windows.Threading;
using System.Windows.Forms;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;

        static DateTime LastPendingTime;
        // Nagyobb-e?
        static string x = "0";
        static double z = 0;
        static List<double> StoredPendings = new List<double>();
        static int counter = 1;
        System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();

        public MainWindow()
        {
            InitializeComponent();

            
            ni.Icon = new System.Drawing.Icon("eye.ico");
            ni.Visible = true;
            ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = System.Windows.WindowState.Normal;
                };
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                this.Hide();
            base.OnStateChanged(e);
            

        }

        public void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Tick += _timer_Tick;
            }

            _timer.Interval = TimeSpan.FromSeconds(30);
            _timer.Start();
            
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            GetIt();
        }

        private void GetIt()
        {
            try
            {
                StartTimer();

                WebClient client = null;
                while (client == null)
                {
                    client = CloudflareEvader.CreateBypassedWebClient("http://pool.burstcoin.ro");
                }

                string json = client.DownloadString("http://pool.burstcoin.ro/pending2.json");

                var result = Regex.Split(json, "\r\n|\r|\n");

                foreach (var item in result)
                {
                    var asd = id.Text;
                    if (item.Contains(asd))
                    {
                        string output = item.Split(':', ',')[1];
                        pending.Content = output;

                        x = pending.Content.ToString();
                        var y = x.Replace('.', ',');
                        y = x.Replace(" ", "");

                        y = y.Replace('.', ',');
                        z = Double.Parse(y);

                        // starts with: count=0
                        StoredPendings.Add(z);
                        // now: count=1

                        if ((StoredPendings.Count > 1) && (StoredPendings[counter - 1] == StoredPendings[counter]))
                        {
                            // MessageBox.Show("A legutóbbi érték változatlan: +" + StoredPendings.Count);
                        }
                        else
                        {
                            LastPendingTime = DateTime.Now;
                            string temp = ("Pending changed to: " + y + " @ " + LastPendingTime);
                            listBox.Items.Add(temp);
                            if (StoredPendings.Count > 1)
                                ni.ShowBalloonTip(5000, "Pending Changed!", Convert.ToString(z), ToolTipIcon.Info);
                        }

                    }
                }
            }
            catch(Exception ex)
            {
                throw new Exception("Hiba! " +ex.Message);
            }
        }

        private void go_Click(object sender, RoutedEventArgs e)
        {
                GetIt();
        }
    }


    public class CloudflareEvader
    {
        /// <summary>
        /// Tries to return a webclient with the neccessary cookies installed to do requests for a cloudflare protected website.
        /// </summary>
        /// <param name="url">The page which is behind cloudflare's anti-dDoS protection</param>
        /// <returns>A WebClient object or null on failure</returns>
        public static WebClient CreateBypassedWebClient(string url)
        {
            var JSEngine = new Jint.Engine(); //Use this JavaScript engine to compute the result.

            //Download the original page
            var uri = new Uri(url);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0";
            //Try to make the usual request first. If this fails with a 503, the page is behind cloudflare.
            try
            {
                var res = req.GetResponse();
                string html = "";
                using (var reader = new StreamReader(res.GetResponseStream()))
                    html = reader.ReadToEnd();
                return new WebClient();
            }
            catch (WebException ex) //We usually get this because of a 503 service not available.
            {
                string html = "";
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    html = reader.ReadToEnd();
                //If we get on the landing page, Cloudflare gives us a User-ID token with the cookie. We need to save that and use it in the next request.
                var cookie_container = new CookieContainer();
                //using a custom function because ex.Response.Cookies returns an empty set ALTHOUGH cookies were sent back.
                var initial_cookies = GetAllCookiesFromHeader(ex.Response.Headers["Set-Cookie"], uri.Host);
                foreach (Cookie init_cookie in initial_cookies)
                    cookie_container.Add(init_cookie);

                /* solve the actual challenge with a bunch of RegEx's. Copy-Pasted from the python scrapper version.*/
                var challenge = Regex.Match(html, "name=\"jschl_vc\" value=\"(\\w+)\"").Groups[1].Value;
                var challenge_pass = Regex.Match(html, "name=\"pass\" value=\"(.+?)\"").Groups[1].Value;

                var builder = Regex.Match(html, @"setTimeout\(function\(\){\s+(var t,r,a,f.+?\r?\n[\s\S]+?a\.value =.+?)\r?\n").Groups[1].Value;
                builder = Regex.Replace(builder, @"a\.value =(.+?) \+ .+?;", "$1");
                builder = Regex.Replace(builder, @"\s{3,}[a-z](?: = |\.).+", "");

                //Format the javascript..
                builder = Regex.Replace(builder, @"[\n\\']", "");

                //Execute it. 
                long solved = long.Parse(JSEngine.Execute(builder).GetCompletionValue().ToObject().ToString());
                solved += uri.Host.Length; //add the length of the domain to it.

                Console.WriteLine("***** SOLVED CHALLENGE ******: " + solved);
                Thread.Sleep(3000); //This sleeping IS requiered or cloudflare will not give you the token!!

                //Retreive the cookies. Prepare the URL for cookie exfiltration.
                string cookie_url = string.Format("{0}://{1}/cdn-cgi/l/chk_jschl", uri.Scheme, uri.Host);
                var uri_builder = new UriBuilder(cookie_url);
                var query = HttpUtility.ParseQueryString(uri_builder.Query);
                //Add our answers to the GET query
                query["jschl_vc"] = challenge;
                query["jschl_answer"] = solved.ToString();
                query["pass"] = challenge_pass;
                uri_builder.Query = query.ToString();

                //Create the actual request to get the security clearance cookie
                HttpWebRequest cookie_req = (HttpWebRequest)WebRequest.Create(uri_builder.Uri);
                cookie_req.AllowAutoRedirect = false;
                cookie_req.CookieContainer = cookie_container;
                cookie_req.Referer = url;
                cookie_req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0";
                //We assume that this request goes through well, so no try-catch
                var cookie_resp = (HttpWebResponse)cookie_req.GetResponse();
                //The response *should* contain the security clearance cookie!
                if (cookie_resp.Cookies.Count != 0) //first check if the HttpWebResponse has picked up the cookie.
                    foreach (Cookie cookie in cookie_resp.Cookies)
                        cookie_container.Add(cookie);
                else //otherwise, use the custom function again
                {
                    //the cookie we *hopefully* received here is the cloudflare security clearance token.
                    if (cookie_resp.Headers["Set-Cookie"] != null)
                    {
                        var cookies_parsed = GetAllCookiesFromHeader(cookie_resp.Headers["Set-Cookie"], uri.Host);
                        foreach (Cookie cookie in cookies_parsed)
                            cookie_container.Add(cookie);
                    }
                    else
                    {
                        //No security clearence? something went wrong.. return null.
                        //Console.WriteLine("MASSIVE ERROR: COULDN'T GET CLOUDFLARE CLEARANCE!");
                        return null;
                    }
                }
                //Create a custom webclient with the two cookies we already acquired.
                WebClient modedWebClient = new WebClientEx(cookie_container);
                modedWebClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0");
                modedWebClient.Headers.Add("Referer", url);
                return modedWebClient;
            }
        }

        /* Credit goes to https://stackoverflow.com/questions/15103513/httpwebresponse-cookies-empty-despite-set-cookie-header-no-redirect 
           (user https://stackoverflow.com/users/541404/cameron-tinker) for these functions 
        */
        public static CookieCollection GetAllCookiesFromHeader(string strHeader, string strHost)
        {
            ArrayList al = new ArrayList();
            CookieCollection cc = new CookieCollection();
            if (strHeader != string.Empty)
            {
                al = ConvertCookieHeaderToArrayList(strHeader);
                cc = ConvertCookieArraysToCookieCollection(al, strHost);
            }
            return cc;
        }

        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');
            ArrayList al = new ArrayList();
            int i = 0;
            int n = strCookTemp.Length;
            while (i < n)
            {
                if (strCookTemp[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    al.Add(strCookTemp[i] + "," + strCookTemp[i + 1]);
                    i = i + 1;
                }
                else
                    al.Add(strCookTemp[i]);
                i = i + 1;
            }
            return al;
        }

        private static CookieCollection ConvertCookieArraysToCookieCollection(ArrayList al, string strHost)
        {
            CookieCollection cc = new CookieCollection();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                string strCNameAndCValue = string.Empty;
                string strPNameAndPValue = string.Empty;
                string strDNameAndDValue = string.Empty;
                string[] NameValuePairTemp;
                Cookie cookTemp = new Cookie();

                for (int j = 0; j < intEachCookPartsCount; j++)
                {
                    if (j == 0)
                    {
                        strCNameAndCValue = strEachCookParts[j];
                        if (strCNameAndCValue != string.Empty)
                        {
                            int firstEqual = strCNameAndCValue.IndexOf("=");
                            string firstName = strCNameAndCValue.Substring(0, firstEqual);
                            string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                            cookTemp.Name = firstName;
                            cookTemp.Value = allValue;
                        }
                        continue;
                    }
                    if (strEachCookParts[j].IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');
                            if (NameValuePairTemp[1] != string.Empty)
                                cookTemp.Path = NameValuePairTemp[1];
                            else
                                cookTemp.Path = "/";
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                                cookTemp.Domain = NameValuePairTemp[1];
                            else
                                cookTemp.Domain = strHost;
                        }
                        continue;
                    }
                }

                if (cookTemp.Path == string.Empty)
                    cookTemp.Path = "/";
                if (cookTemp.Domain == string.Empty)
                    cookTemp.Domain = strHost;
                cc.Add(cookTemp);
            }
            return cc;
        }
    }

    public class WebClientEx : WebClient
    {
        public WebClientEx(CookieContainer container)
        {
            this.container = container;
        }

        public CookieContainer CookieContainer
        {
            get { return container; }
            set { container = value; }
        }

        private CookieContainer container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            var request = r as HttpWebRequest;
            if (request != null)
            {
                request.CookieContainer = container;
            }
            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        private void ReadCookies(WebResponse r)
        {
            var response = r as HttpWebResponse;
            if (response != null)
            {
                CookieCollection cookies = response.Cookies;
                container.Add(cookies);
            }
        }

    }

}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Drawing.Drawing2D;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Threading;
using Microsoft.Win32;
using System.Diagnostics;
//using CefSharp;
//using CefSharp.WinForms;
using Oracle.ManagedDataAccess.Client;
using ZL_BJCAAllinterface;
namespace CriticalValueSystem
{
    public partial class Form1 : Form
    {
        private int heartbeatFailureCount = 0;//如果连续失败次数超过阈值（如 3 次），则尝试重新获取 Token 或重新连接服务端。
        private static readonly HttpClient httpClient = new HttpClient();
        private string accessToken = string.Empty;
        private DateTime tokenExpirationTime = DateTime.MinValue; // Token 过期时间

        private string TimeoutaccessToken = string.Empty;
        private DateTime TimeouttokenExpirationTime = DateTime.MinValue; // Token 过期时间

        private System.Timers.Timer heartbeatTimer;
        private NotifyIcon trayIcon; // 托盘图标
        private ContextMenuStrip trayMenu; // 托盘右键菜单
        //private List<CriticalValue> criticalValues = new List<CriticalValue>(); // 危急值数据列表
        //private const string SecretKey = "Wju7wvER2kXTF0vjAlxDzAolO9Qgkn5rgqGjds6vqH399chcUaiMkqfK7602kbl6"; // 测试密钥
        private HttpListener httpListener;
        private HashSet<CriticalValue> criticalValues = new HashSet<CriticalValue>();
        private readonly object criticalValuesLock = new object(); // 用于加锁的对象
        //控制闪烁
        private  System.Windows.Forms.Timer blinkTimer; // 用于控制背景闪烁的 Timer
        private Color originalBackColor; // 保存 ListBox 的原始背景颜色
        private bool isBlinking = false; // 标记是否处于闪烁状态

        private const int ButtonWidth = 80; // 按钮宽度
        private const int ButtonHeight = 20; // 按钮高度
        private const int ButtonPadding = 5; // 按钮与文本的间距
        //查询服务器中未确认的数据  定时器
        // Add near other field declarations at the top of Form1 class
        private System.Timers.Timer pollingTimer;
        private bool isPolling = false; // Flag to prevent concurrent polls
        private readonly object pollingLock = new object(); // Lock for the flag

        private readonly string UpdateCheckUrl = ConfigurationManager.AppSettings["UpdateCheckUrl"];
        private readonly string UpdaterExecutableName = ConfigurationManager.AppSettings["UpdaterExecutableName"];

        // 在 Form1 类中添加此字段
        private DownloadProgressForm _downloadProgressForm;
        // 在 Form1 类的顶部，与其他成员变量一起声明
        private bool serverAllowsButtonVisibility = true; // 默认为可见，直到从服务器获取设置为止
        private readonly object buttonVisibilityLock = new object(); // 用于锁定访问 serverAllowsButtonVisibility

        // 在 Form1 类的顶部，与其他成员变量一起声明
        private bool requiresTimeoutAction = false; // 默认超时不需要强制补救措施
        private readonly object timeoutActionLock = new object(); // 用于锁定访问 requiresTimeoutAction

        // 在 Form1 类的顶部，与其他成员变量一起声明
        private bool enabledCa = false;    //医技科室默认不开启CA
        private readonly object enabledCALock = new object(); // 用于锁定访问 enabledCa

        // 在 Form1 类的顶部，与其他成员变量一起声明
        private int currentPendingProcessingCount = 0; // 当前待处理数量，默认为0
        private readonly object processingCountLock = new object(); // 用于锁定访问 currentPendingProcessingCount

        // 在 Form1 类的顶部，与其他成员变量一起声明
        private List<CriticalValue> displayedCriticalValues = new List<CriticalValue>(); // 存储当前显示在ListBox中的、已排序的危急值列表
                            // 注意：这个列表只在UI线程的UpdateCriticalValueList方法中更新，
                            // 在UI事件(MouseClick)中读取，因此通常不需要额外的锁，但访问时需要小心。


        // 从配置文件读取配置
        private readonly string SecretKey;
        private readonly string ApiBaseUrl;
        private readonly string TokenEndpoint;
        private readonly string HeartbeatEndpoint;
        private readonly string WebAppUrl;
        private readonly string HomeUrl;
        private readonly string ClientType;
        //private readonly string PublicKey;
        // 用户登录信息
        private UserLoginInfo currentUser;
        private bool isUserLoggedIn = false;
        private readonly ManualResetEvent loginWaitHandle = new ManualResetEvent(false);

        private Panel titleBar;// 自定义标题栏
        private Label titleLabel;
        private Button homeButton;// 首页按钮
        private Button closeButton;
        private Button minimizeButton;
        private Panel contentPanel; // 新增内容面板

        // 在Form1类顶部添加字段保存当前用户名
        private string currentUserName = string.Empty;

        private Dictionary<string, Process> openBrowserProcesses = new Dictionary<string, Process>();
        private Dictionary<string, Form> browserForms = new Dictionary<string, Form>();

        // 在Form1类中定义CefSharp浏览器
        //private ChromiumWebBrowser cefBrowser;

        public Form1()
        {
            // 在构造函数中初始化配置值
            SecretKey = ConfigurationManager.AppSettings["SecretKey"];
            ApiBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"];
            TokenEndpoint = ConfigurationManager.AppSettings["TokenEndpoint"];
            HeartbeatEndpoint = ConfigurationManager.AppSettings["HeartbeatEndpoint"];
            WebAppUrl = ConfigurationManager.AppSettings["WebAppUrl"];
            HomeUrl = ConfigurationManager.AppSettings["HomeUrl"];
            ClientType= ConfigurationManager.AppSettings["ClientType"];
            // 新增：验证RSA公钥配置
            try
            {
                // 提前验证公钥是否有效
                var testKey = RsaHelper.PublicKey;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RSA公钥配置无效: {ex.Message}", "配置错误",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
            // 初始化用户信息
            currentUser = new UserLoginInfo();

            InitializeComponent();


            // 先设置窗体属性
            //在创建自定义标题栏时，由于我们不再需要默认的标题栏，因此需要将窗体的边框样式设置为None
            //。这样，您就可以完全控制窗体的外观。
            this.FormBorderStyle = FormBorderStyle.None;
            this.Padding = new Padding(2); // 窗体边框
            this.BackColor = Color.FromArgb(64, 64, 64); // 边框颜色
            //this.TopMost = true;
            // 创建自定义标题栏
            CreateCustomTitleBar();
  

            // 初始化托盘图标
            InitializeTrayIcon();
            // 窗体加载时初始化定时器
           InitializeTimer();

            InitializePollingTimer(); // Initialize the new timer
            // 启动时最小化到托盘
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;


            //// 设置窗体启动位置为右下角
            //this.StartPosition = FormStartPosition.Manual;
            //SetFormPositionToBottomRight();
            //// 禁用最大化按钮
            //this.MaximizeBox = false;
            //// 设置窗体边框样式为固定大小
            //this.FormBorderStyle = FormBorderStyle.FixedSingle;
            //禁用最大化按钮,设置窗体边框样式为固定大小,禁用双击标题栏最大化
            DisableMaximize();
            // 设置 ListBox 的 Dock 属性，使其填充整个窗体
            //lstCriticalValues.Dock = DockStyle.Fill;


            // 初始化闪烁 Timer
            blinkTimer = new System.Windows.Forms.Timer();
            blinkTimer.Interval = 500; // 设置闪烁间隔（500 毫秒）
            blinkTimer.Tick += BlinkTimer_Tick;







            // 设置 ListBox 的字体和样式
            lstCriticalValues.Font = new Font("微软雅黑", 12);
            lstCriticalValues.BackColor = Color.White;
            lstCriticalValues.ForeColor = Color.Black;

            // 设置窗体的最小大小，避免窗体过小导致内容显示不全



            // 设置 ListBox 为自定义绘制模式
            lstCriticalValues.DrawMode = DrawMode.OwnerDrawVariable;
            lstCriticalValues.DrawItem += LstCriticalValues_DrawItem;
            lstCriticalValues.MeasureItem += LstCriticalValues_MeasureItem;
            lstCriticalValues.MouseClick += LstCriticalValues_MouseClick;

           
            // 保存 ListBox 的原始背景颜色
            originalBackColor = lstCriticalValues.BackColor;

            // 设置窗体标题为默认值
            this.Text = "危急值客户端";
            // 重要：移除窗体的自动缩放，防止布局问题
            this.AutoScaleMode = AutoScaleMode.None;

            // 订阅窗体大小改变事件
            this.Resize += Form1_Resize;
        }
        // 创建自定义标题栏
        //使用TableLayoutPanel精确控制布局
        private void CreateCustomTitleBar()
        {
            // 先清除所有控件，重新布局
            this.Controls.Clear();

            // 创建主布局表格
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(2),
                Margin = new Padding(0),
                BackColor = this.BackColor
            };

            // 设置行高
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // 标题栏高度
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 内容区域高度
            // 创建标题栏
            titleBar = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 35,
                BackColor = Color.FromArgb(45, 45, 48),
                Margin = new Padding(0)
            };

            // 标题文本
            titleLabel = new Label
            {
                Text = "危急值客户端",
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 10, FontStyle.Regular),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            // 首页按钮
            homeButton = new Button
            {
                Text = "首页",
                FlatStyle = FlatStyle.Flat,
                Width = 60,
                Dock = DockStyle.Right,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 48),
                FlatAppearance = { BorderSize = 0 }
            };
            homeButton.Click += HomeButton_ClickAsync;

            // 关闭按钮
            closeButton = new Button
            {
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                Width = 45,
                Dock = DockStyle.Right,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 48),
                FlatAppearance = { BorderSize = 0 }
            };
            closeButton.Click += (s, e) => this.Hide();

            // 最小化按钮
            minimizeButton = new Button
            {
                Text = "─",
                FlatStyle = FlatStyle.Flat,
                Width = 45,
                Dock = DockStyle.Right,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 48),
                FlatAppearance = { BorderSize = 0 }
            };
            //minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            minimizeButton.Click += (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = true; // 确保最小化时在任务栏显示
            };
            // 添加控件到标题栏，注意添加顺序（先添加的在下层）
            titleBar.Controls.Add(titleLabel);       // 先添加标题
            titleBar.Controls.Add(closeButton);      // 再添加关闭按钮（最右）
            titleBar.Controls.Add(minimizeButton);   // 再添加最小化按钮
            titleBar.Controls.Add(homeButton);       // 最后添加首页按钮

            // 创建内容面板
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5),
                Margin = new Padding(0)
            };

            // 将ListBox添加到内容面板
            contentPanel.Controls.Add(lstCriticalValues);
            lstCriticalValues.Dock = DockStyle.Fill;

            // 将标题栏和内容面板添加到布局表格
            mainLayout.Controls.Add(titleBar, 0, 0);
            mainLayout.Controls.Add(contentPanel, 0, 1);

            // 将布局表格添加到窗体
            this.Controls.Add(mainLayout);




            // 添加拖动事件
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            // 标题标签也需要拖动事件
            titleLabel.MouseDown += TitleBar_MouseDown;
            titleLabel.MouseMove += TitleBar_MouseMove;
            titleLabel.MouseUp += TitleBar_MouseUp;

            // 在最后添加初始化按钮可见性
           // UpdateButtonsVisibility();

        }

        // 添加窗体大小改变事件处理
        private void Form1_Resize(object sender, EventArgs e)
        {
            // 手动更新ListBox大小
            lstCriticalValues.Size = new Size(
                this.ClientSize.Width - 10,
                this.ClientSize.Height - titleBar.Height - 10
            );
        }

        public void EnsureFormTopMost()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(EnsureFormTopMost));
                return;
            }

            this.TopMost = true;
        }


        // 新增方法：从服务器获取按钮可见性配置
        private async Task FetchButtonVisibilitySettingAsync()
        {
            // 确保已登录且有Token
            if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine($"{DateTime.Now}: 获取按钮可见性设置失败 - 未登录或Token无效。");
                // 可以在这里决定是否应用默认值或保持当前状态
                // ApplyCurrentButtonVisibilitySetting(); // 应用当前存储的值（或默认值）
                return;
            }

            string configKey = "critical.client.close"; // 配置项的Key
            string apiUrl = $"{ApiBaseUrl}/system/config/configKey/{configKey}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    // 添加认证头（如果需要）
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    Console.WriteLine($"{DateTime.Now}: 正在获取按钮可见性设置 ({configKey})...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var configResponse = JsonConvert.DeserializeObject<ConfigResponse>(responseBody);

                        if (configResponse != null && configResponse.Code == 200)
                        {
                            bool newSetting = configResponse.Message == "1"; // "1" 表示可见
                            bool changed = false;

                            lock (buttonVisibilityLock)
                            {
                                if (serverAllowsButtonVisibility != newSetting)
                                {
                                    serverAllowsButtonVisibility = newSetting;
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器按钮可见性设置更新为: {newSetting}，正在应用...");
                                // 在UI线程上更新按钮可见性
                                ApplyCurrentButtonVisibilitySetting();
                            }
                            else
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器按钮可见性设置未改变 ({newSetting}).");
                                // 即使未改变，也最好应用一次，确保初始状态正确
                                ApplyCurrentButtonVisibilitySetting();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 获取按钮可见性设置失败 - API响应无效。 Code: {configResponse?.Code}, Msg: {configResponse?.Message}");
                            // 获取失败时，可以决定是保持上次的值还是使用默认值
                            ApplyCurrentButtonVisibilitySetting(); // 应用当前（或默认）设置
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 获取按钮可见性设置失败 - HTTP状态码: {response.StatusCode}");
                        // 获取失败时处理
                        ApplyCurrentButtonVisibilitySetting(); // 应用当前（或默认）设置
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 获取按钮可见性设置时发生异常: {ex.Message}");
                // 异常时处理
                ApplyCurrentButtonVisibilitySetting(); // 应用当前（或默认）设置
            }
        }


        // 重命名并修改 UpdateButtonsVisibility 方法
        /// <summary>
        /// 应用按钮可见性设置，优先考虑列表是否为空。
        /// </summary>
        private void ApplyCurrentButtonVisibilitySetting()
        {
            if (this.InvokeRequired)
            {
                // 使用 BeginInvoke 避免阻塞
                this.BeginInvoke(new Action(ApplyCurrentButtonVisibilitySetting));
                return;
            }

            bool isListEmpty;
            // 1. 检查列表是否为空 (需要加锁访问共享资源)
            lock (criticalValuesLock)
            {
                isListEmpty = criticalValues.Count == 0;
            }

            bool shouldShowButtons;

            // 2. 决定最终的按钮可见性
            if (isListEmpty)
            {
                // 列表为空时，强制显示按钮
                shouldShowButtons = true;
                Console.WriteLine($"按钮可见性应用: 列表为空，强制显示按钮。");
            }
            else
            {
                //医生默认不给关,但是可以控制
                if (currentUser.Nature.Equals("1"))
                {

                    // 列表不为空时，读取并应用服务器的设置
                    lock (buttonVisibilityLock) // 加锁访问共享的服务器设置变量
                    {
                        shouldShowButtons = serverAllowsButtonVisibility;
                    }
                    Console.WriteLine($"按钮可见性应用: 列表有数据，根据服务器设置决定显示={shouldShowButtons} (服务器允许={serverAllowsButtonVisibility})");

                }
                else 
                {
                    shouldShowButtons = true;

                }

                //护士给关

            }

            // 3. 应用最终的可见性到按钮控件
            if (minimizeButton != null)
            {
                // 检查控件是否已被释放，防止 ObjectDisposedException
                if (!minimizeButton.IsDisposed)
                {
                    minimizeButton.Visible = shouldShowButtons;
                }
                else
                {
                    Console.WriteLine("警告: 最小化按钮已被释放，无法设置可见性。");
                }
            }

            if (closeButton != null)
            {
                if (!closeButton.IsDisposed)
                {
                    closeButton.Visible = shouldShowButtons;
                }
                else
                {
                    Console.WriteLine("警告: 关闭按钮已被释放，无法设置可见性。");
                }
            }
        }

        // 新增方法：从服务器获取超时是否需要补救措施的配置
        private async Task FetchTimeoutActionSettingAsync()
        {
            // 确保已登录且有Token
            if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine($"{DateTime.Now}: 获取超时补救设置失败 - 未登录或Token无效。");
                return;
            }

            string configKey = "critical.timeout.action"; // 配置项的Key
            string apiUrl = $"{ApiBaseUrl}/system/config/configKey/{configKey}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    Console.WriteLine($"{DateTime.Now}: 正在获取超时补救设置 ({configKey})...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        // 可以复用之前的 ConfigResponse 模型
                        var configResponse = JsonConvert.DeserializeObject<ConfigResponse>(responseBody);

                        if (configResponse != null && configResponse.Code == 200)
                        {
                            bool newSetting = configResponse.Message == "1"; // "1" 表示需要补救措施
                            bool changed = false;

                            lock (timeoutActionLock)
                            {
                                if (requiresTimeoutAction != newSetting)
                                {
                                    requiresTimeoutAction = newSetting;
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器超时补救设置更新为: {newSetting}");
                            }
                            else
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器超时补救设置未改变 ({newSetting}).");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 获取超时补救设置失败 - API响应无效。 Code: {configResponse?.Code}, Msg: {configResponse?.Message}");
                            // 可考虑设置默认值或保持不变
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 获取超时补救设置失败 - HTTP状态码: {response.StatusCode}");
                        // 可考虑设置默认值或保持不变
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 获取超时补救设置时发生异常: {ex.Message}");
                // 可考虑设置默认值或保持不变
            }
        }
        // 新增方法：从服务器获取医技科室是否启用CA的配置
        private async Task FetchEnabledCaSettingAsync()
        {
            // 确保已登录且有Token
            if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine($"{DateTime.Now}: 获取医技科室是否启用CA设置失败 - 未登录或Token无效。");
                return;
            }

            string configKey = "critical.yjdept.ca"; // 配置项的Key
            string apiUrl = $"{ApiBaseUrl}/system/config/configKey/{configKey}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    Console.WriteLine($"{DateTime.Now}: 医技科室是否启用CA ({configKey})...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        // 可以复用之前的 ConfigResponse 模型
                        var configResponse = JsonConvert.DeserializeObject<ConfigResponse>(responseBody);

                        if (configResponse != null && configResponse.Code == 200)
                        {
                            bool newSetting = configResponse.Message == "1"; // "1" 表示需要启用CA
                            bool changed = false;

                            lock (enabledCALock)
                            {
                                if (enabledCa != newSetting)
                                {
                                    enabledCa = newSetting;
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器医技科室启用CA设置更新为: {newSetting}");
                            }
                            else
                            {
                                Console.WriteLine($"{DateTime.Now}: 服务器医技科室启用CA未改变 ({newSetting}).");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 获取医技科室启用CA失败 - API响应无效。 Code: {configResponse?.Code}, Msg: {configResponse?.Message}");
                            // 可考虑设置默认值或保持不变
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 获取医技科室启用CA设置失败 - HTTP状态码: {response.StatusCode}");
                        // 可考虑设置默认值或保持不变
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 获取医技科室启用CA设置时发生异常: {ex.Message}");
                // 可考虑设置默认值或保持不变
            }
        }


        /// <summary>
        /// 根据危急值列表中是否有数据来更新按钮的可见性
        /// </summary>
        private void UpdateButtonsVisibility()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(UpdateButtonsVisibility));
                return;
            }

            // 检查是否有危急值数据
            bool hasData = false;
            lock (criticalValuesLock)
            {
                hasData = criticalValues.Count > 0;
            }

            // 根据数据状态设置按钮可见性
            if (minimizeButton != null)
                minimizeButton.Visible = !hasData;

            if (closeButton != null)
                closeButton.Visible = !hasData;

            // 记录状态便于调试
            Console.WriteLine($"按钮可见性更新: 有数据={hasData}, 按钮隐藏={hasData}");
        }


        // 更新标题方法（包含用户名）
        //public void UpdateFormTitle(string userName = null)
        //{
        //    if (this.InvokeRequired)
        //    {
        //        this.Invoke(new Action<string>(UpdateFormTitle), userName);
        //        return;
        //    }

        //    if (!string.IsNullOrEmpty(userName))
        //    {
        //        titleLabel.Text = $"危急值客户端 - 登录人: {userName}";
        //    }
        //    else
        //    {
        //        titleLabel.Text = "危急值客户端";
        //    }
        //}

        // 更新标题方法，包含用户名和危急值数量
        //public void UpdateFormTitle(string userName = null, int? criticalValueCount = null)
        public void UpdateFormTitle(string userName = null, int? pendingConfirmationCount = null, int? pendingProcessingCount = null)
        {
            if (titleLabel == null || titleLabel.IsDisposed)
            {
                return; // 标题标签无效
            }

            try
            {
                if (this.InvokeRequired)
                {
                    //this.BeginInvoke(new Action<string, int?>(UpdateFormTitle), userName, criticalValueCount);
                    this.BeginInvoke(new Action<string, int?, int?>(UpdateFormTitle), userName, pendingConfirmationCount, pendingProcessingCount);
                    return;
                }

                // 保存用户名（如果提供了）
                if (userName != null)
                {
                    currentUserName = userName;
                }

                // 获取当前未确认数量（如果未提供）
                int currentConfirmationCount = pendingConfirmationCount ?? GetUnprocessedCriticalValueCount();

                // 获取当前待处理数量（如果未提供，则使用成员变量中存储的值）
                int currentProcessingCount;
                if (pendingProcessingCount.HasValue)
                {
                    currentProcessingCount = pendingProcessingCount.Value;
                    // 更新成员变量的值
                    lock (processingCountLock)
                    {
                        currentPendingProcessingCount = currentProcessingCount;
                    }
                }
                else
                {
                    // 如果没提供新的值，使用上次获取或默认的值
                    lock (processingCountLock)
                    {
                        currentProcessingCount = currentPendingProcessingCount;
                    }
                }
                // 构建标题文本
                //string titleText = "危急值客户端";
                //if (!string.IsNullOrEmpty(currentUserName))
                //{
                //    titleText = $"危急值客户端 - 登录人：{currentUserName} 当前未确认 {currentCount}条 当前未处理多少条，请及时进入首页处理";
                //}
                // 构建标题文本
                StringBuilder titleBuilder = new StringBuilder("危急值客户端");
                if (!string.IsNullOrEmpty(currentUserName))
                {
                    titleBuilder.Append($" - 登录人：{currentUserName}");
                    // 添加未处理（待确认）数量
                    titleBuilder.Append($" 当前待确认 {currentConfirmationCount}条");

                    // 如果待处理数量大于0，则添加待处理信息
                    if (currentProcessingCount > 0)
                    {
                        titleBuilder.Append($", 当前待处理 {currentProcessingCount}条请及时进入首页处理");
                    }

                }



                string titleText = titleBuilder.ToString();
                // 更新标题
                titleLabel.Text = titleText;
                this.Text = titleText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新标题异常: {ex.Message}");
            }
        }

        // 直接更新标题的方法，便于更好地处理错误
        private void UpdateFormTitleDirect(string userName)
        {
            // 先检查标题标签是否有效
            if (titleLabel == null || titleLabel.IsDisposed)
            {
                Console.WriteLine("标题标签为空或已释放");
                return;
            }

            // 设置窗体标题（作为备份，以防自定义标题失效）
            if (!string.IsNullOrEmpty(userName))
            {
                int unprocessedCount = criticalValues.Count(v => !v.IsProcessed);
                this.Text = $"危急值客户端 - 登录人: {userName} 当前未确认 {unprocessedCount}";
                titleLabel.Text = $"危急值客户端 - 登录人: {userName} 当前未确认 {unprocessedCount}";
            }
            else
            {
                this.Text = "危急值客户端";
                titleLabel.Text = "危急值客户端";
            }

            // 确保置顶
            this.TopMost = true;
        }
        // 首页按钮点击事件
        //private void HomeButton_Click(object sender, EventArgs e)
        //{

        //    if (APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"1\"}", out string strInfo))
        //    {

        //        // CA签名后获取Token
        //        await TimeoutGetAccessTokenAsync(strInfo);
        //     }           
        //        if (string.IsNullOrEmpty(accessToken))
        //    {
        //        MessageBox.Show("未登录或Token无效，请先登录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return;
        //    }

        //    //string url = $"{WebAppUrl}?token={accessToken}";

        //    string url = $"{HomeUrl}{accessToken}";
        //    System.Diagnostics.Process.Start(url);
        //}
        // 1. 修改方法签名为 async void
        private async void HomeButton_ClickAsync(object sender, EventArgs e)
        {
            // 2. 添加 try-catch 块来捕获所有可能的异常
            Button clickedButton = sender as Button; // 获取被点击的按钮
            if (clickedButton != null)
            {
                clickedButton.Enabled = false; // 防止重复点击
            }

            try
            {
                // 确保 currentUser 和 UserId 有效
                //if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                //{
                //    MessageBox.Show("当前用户信息无效，无法执行操作。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //    return; //提前返回前，确保按钮状态已处理 (见 finally)
                //}

                // 假设 APP_CheckCertificate 是一个同步方法，如果它可能抛出异常，也应考虑在内
                if (APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"1\"}", out string strInfo))
                {
                    // CA签名后获取Token
                    await TimeoutGetAccessTokenAsync(strInfo); // 这是一个异步调用

                    // TimeoutaccessToken 应该是您类中的一个字段或属性，由 TimeoutGetAccessTokenAsync 填充
                    if (string.IsNullOrEmpty(this.TimeoutaccessToken)) // 假设 TimeoutaccessToken 是类成员
                    {
                        MessageBox.Show("未能获取有效的访问令牌，请稍后重试或检查登录状态。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return; //提前返回前，确保按钮状态已处理 (见 finally)
                    }

                    if (string.IsNullOrEmpty(HomeUrl))
                    {
                        MessageBox.Show("首页地址未配置。", "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return; //提前返回前，确保按钮状态已处理 (见 finally)
                    }

                    string url = $"{HomeUrl}{this.TimeoutaccessToken}"; // 使用 this.TimeoutaccessToken
                    Console.WriteLine($"HomeButton: Opening URL: {url}"); // 调试日志

                    // 使用 ProcessStartInfo 以更好地控制，并确保 UseShellExecute = true 以打开默认浏览器
                    ProcessStartInfo psi = new ProcessStartInfo(url)
                    {
                        UseShellExecute = true // 这对于打开URL到默认浏览器很重要
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    // APP_CheckCertificate 返回 false，可以根据 strInfo 显示更详细的错误
                    MessageBox.Show($"证书验证失败或操作被取消。详情: {strInfo}", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                // Process.Start 可能会因为找不到关联程序等原因抛出此异常
                MessageBox.Show($"无法打开链接或执行操作：{winEx.Message}", "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"HomeButton_ClickAsync Win32Exception: {winEx.ToString()}");
            }
            catch (Exception ex)
            {
                // 捕获其他所有类型的异常
                MessageBox.Show($"执行首页操作时发生未知错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"HomeButton_ClickAsync Exception: {ex.ToString()}");
            }
            finally
            {
                if (clickedButton != null)
                {
                    clickedButton.Enabled = true; // 无论成功与否，最后都恢复按钮可用状态
                }
            }
        }


        // 标题栏拖动实现
        private bool isDragging = false;
        private Point dragStartPoint;

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = new Point(e.X, e.Y);
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point p = PointToScreen(new Point(e.X, e.Y));

                // 计算偏移，控件可能不是从窗体左上角开始
                int offsetX = 0;
                int offsetY = 0;

                // 如果事件来自标题标签，需要计算其位置偏移
                if (sender == titleLabel)
                {
                    offsetX = titleLabel.Left;
                    offsetY = titleLabel.Top;
                }

                this.Location = new Point(
                    p.X - dragStartPoint.X - offsetX,
                    p.Y - dragStartPoint.Y - offsetY
                );
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }
        // Timer Tick 事件，实现闪烁效果
        //private void BlinkTimer_Tick(object sender, EventArgs e)
        //{
        //    Console.WriteLine("Tick event triggered!"); // 调试信息
        //    //MessageBox();
        //    if (lstCriticalValues.InvokeRequired) // 检查是否需要跨线程调用
        //    {
        //        lstCriticalValues.Invoke(new Action(() => BlinkTimer_Tick(sender, e)));
        //        return;
        //    }

        //    if (lstCriticalValues.BackColor == originalBackColor)
        //    {
        //        lstCriticalValues.BackColor = Color.Red; // 闪烁颜色
        //    }
        //    else
        //    {
        //        lstCriticalValues.BackColor = originalBackColor;
        //    }
        //}
        // Timer Tick 事件，实现闪烁效果
        private void BlinkTimer_Tick(object sender, EventArgs e)
        {

            if (lstCriticalValues.InvokeRequired) // 检查是否需要跨线程调用
            {
                lstCriticalValues.Invoke(new Action(() => BlinkTimer_Tick(sender, e)));
                return;
            }

            //Console.WriteLine("Tick event triggered!"); // 调试信息

            if (lstCriticalValues.BackColor == originalBackColor)
            {
                lstCriticalValues.BackColor = Color.Red; // 闪烁颜色
            }
            else
            {
                lstCriticalValues.BackColor = originalBackColor;
            }
        }


        // 开始闪烁
        private void StartBlinking()
        {
            if (!isBlinking)
            {
                isBlinking = true;
                blinkTimer.Start();
              //  Console.WriteLine("Timer started!"); // 调试信息
            }
        }

        // 停止闪烁并恢复背景颜色
        private void StopBlinking()
        {
            if (isBlinking)
            {
                isBlinking = false;
                blinkTimer.Stop();
                lstCriticalValues.BackColor = originalBackColor;
                Console.WriteLine("Timer stopped!"); // 调试信息
            }
        }



        private void DisableMaximize()
        {
            // 禁用最大化按钮
            this.MaximizeBox = false;

            // 禁用双击标题栏最大化
            this.DoubleBuffered = true; // 防止闪烁
            this.MaximumSize = this.Size; // 限制窗体大小为当前大小
        }
        //protected override void WndProc(ref Message m)
        //{
        //    const int WM_NCLBUTTONDBLCLK = 0x00A3; // 非客户区双击事件

        //    if (m.Msg == WM_NCLBUTTONDBLCLK)
        //    {
        //        // 拦截双击标题栏的事件，不执行任何操作
        //        return;
        //    }

        //    base.WndProc(ref m);
        //}
        //private void ShowAndFocusForm()
        //{
        //    // 窗体居中显示
        //    this.StartPosition = FormStartPosition.CenterScreen;
        //    this.TopMost = true; // 置顶
        //    this.Show(); // 确保窗体显示
        //    this.Activate(); // 激活窗体
        //}
        // 窗体居中显示并置顶
        private void ShowAndFocusForm()
        {
            if (this.InvokeRequired)
            {
                // 使用 BeginInvoke 异步调用，避免阻塞当前线程
                this.BeginInvoke(new Action(ShowAndFocusForm));
                return;
            }

            // 如果窗体被最小化到托盘，恢复窗体状态
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }

            // 确保窗体显示并置顶
            this.StartPosition = FormStartPosition.CenterScreen; // 窗体居中显示
            this.ShowInTaskbar = true;//控制窗体是否在任务栏中显示。
            //this.ControlBox = false;//窗体上面的最大最小关闭按钮
            this.TopMost = true; // 置顶
            this.Show(); // 显示窗体
            this.Activate(); // 激活窗体
            this.BringToFront(); // 将窗体置于最前

            // 再次确保窗体置顶（有时系统会改变此属性）
            Application.DoEvents();
            this.TopMost = true;
        }

        private void LstCriticalValues_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstCriticalValues.Items.Count)
                return;

            e.DrawBackground();
            string itemText = lstCriticalValues.Items[e.Index].ToString();
            Brush textBrush = new SolidBrush(e.ForeColor);

            // 计算文本区域，为两个按钮留出空间
            int buttonsWidth = 170; // 两个按钮的总宽度加间距
            Rectangle textRect = new Rectangle(
            e.Bounds.Left,
            e.Bounds.Top,
            e.Bounds.Width - buttonsWidth,
            e.Bounds.Height);

            // 绘制文本
            e.Graphics.DrawString(itemText, e.Font, textBrush, textRect);

            // 按钮尺寸和位置
            int buttonWidth = 90; // 按钮宽度
            int buttonHeight = 25; // 按钮高度
            int buttonPadding = 5; // 按钮与右侧的间距
            int buttonSpacing = 10; // 按钮之间的间距

            // 确认并处理按钮
            Rectangle processButtonRect = new Rectangle(
            e.Bounds.Right - buttonWidth - buttonPadding,
            e.Bounds.Top + (e.Bounds.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);

            // 确认按钮
            Rectangle confirmButtonRect = new Rectangle(
            processButtonRect.Left - buttonWidth - buttonSpacing,
            e.Bounds.Top + (e.Bounds.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);

            // 绘制确认按钮
            using (LinearGradientBrush confirmBrush = new LinearGradientBrush(
            confirmButtonRect,
            Color.FromArgb(255, 100, 200, 100), // 绿色渐变起始颜色
            Color.FromArgb(255, 50, 150, 50), // 绿色渐变结束颜色
            LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(confirmBrush, confirmButtonRect);
            }

            // 绘制确认并处理按钮
            using (LinearGradientBrush processBrush = new LinearGradientBrush(
            processButtonRect,
            Color.FromArgb(255, 100, 150, 255), // 蓝色渐变起始颜色
            Color.FromArgb(255, 50, 100, 200), // 蓝色渐变结束颜色
            LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(processBrush, processButtonRect);
            }

            // 绘制按钮边框（圆角）
            using (Pen pen = new Pen(Color.FromArgb(255, 30, 80, 150), 1))
            {
                // 绘制确认按钮边框
                DrawRoundedRectangle(e.Graphics, confirmButtonRect, 5, pen);

                // 绘制确认并处理按钮边框
                DrawRoundedRectangle(e.Graphics, processButtonRect, 5, pen);
            }

            // 按钮文本（居中）
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                // 绘制确认按钮文本
                e.Graphics.DrawString("确认", e.Font, Brushes.White, confirmButtonRect, sf);

                // 绘制确认并处理按钮文本
                e.Graphics.DrawString("确认并处理", e.Font, Brushes.White, processButtonRect, sf);
            }

            e.DrawFocusRectangle();
        }
        // 辅助方法：绘制圆角矩形
        private void DrawRoundedRectangle(Graphics g, Rectangle rect, int cornerRadius, Pen pen)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
            path.AddArc(rect.Right - cornerRadius * 2, rect.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
            path.AddArc(rect.Right - cornerRadius * 2, rect.Bottom - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }


        private void LstCriticalValues_MeasureItem(object sender, MeasureItemEventArgs e)
        {
             e.ItemHeight = 30; // 设置每项的高度
            // 计算每个项目所需的高度
            //string itemText = lstCriticalValues.Items[e.Index].ToString();
            //using (Graphics g = lstCriticalValues.CreateGraphics())
            //{
            //    SizeF textSize = g.MeasureString(itemText, lstCriticalValues.Font);
            //    e.ItemHeight = (int)Math.Ceiling(textSize.Height) + 4; // 增加一些额外的间距
            //}

        }

        // 2. 修改鼠标点击事件处理，区分两个按钮的点击

        private void LstCriticalValues_MouseClick(object sender, MouseEventArgs e)
        {
            int index = lstCriticalValues.IndexFromPoint(e.Location);
            if (index < 0 || index >= lstCriticalValues.Items.Count)
                return;

            // **** 修改点：从 displayedCriticalValues 中获取对象 ****
            CriticalValue value = null;
            // 因为 displayedCriticalValues 是在 UI 线程更新和读取的，
            // 在 UI 事件中访问通常是安全的，无需 criticalValuesLock。
            // 但是为了安全起见，可以检查索引范围。
            if (index >= 0 && index < displayedCriticalValues.Count)
            {
                value = displayedCriticalValues[index];
            }

            if (value == null) // 如果没有获取到对象，直接返回
                return;

            // 计算按钮区域
            Rectangle itemRect = lstCriticalValues.GetItemRectangle(index);

            // 按钮尺寸和位置
            int buttonWidth = 90; // 按钮宽度
            int buttonHeight = 24; // 按钮高度
            int buttonPadding = 5; // 按钮与右侧的间距
            int buttonSpacing = 10; // 按钮之间的间距

            // 确认并处理按钮
            Rectangle processButtonRect = new Rectangle(
            itemRect.Right - buttonWidth - buttonPadding,
            itemRect.Top + (itemRect.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);

            // 确认按钮
            Rectangle confirmButtonRect = new Rectangle(
            processButtonRect.Left - buttonWidth - buttonSpacing,
            itemRect.Top + (itemRect.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);

            // 获取对应的危急值对象
            //CriticalValue value = null;
            //lock (criticalValuesLock)
            //{
            //    value = criticalValues.ElementAtOrDefault(index);
            //}

            //if (value == null)
            //    return;

            // 判断点击的是哪个按钮
            if (confirmButtonRect.Contains(e.Location))
            {
                // 点击了确认按钮
                ConfirmCriticalValue(value);
            }
            else if (processButtonRect.Contains(e.Location))
            {
                // 点击了确认并处理按钮
                ConfirmAndProcessCriticalValue(value);
            }
        }

        // 3. 添加确认危急值的方法

        //private async void ConfirmCriticalValue(CriticalValue value)
        //{
        //    try
        //    {
        //        // 构造请求参数
        //        var requestData = new
        //        {
        //            doctorId = currentUser.UserId,
        //            criticalId = value.criticalId,
        //            confirmResult= "确认有效"
        //        };

        //        // 将参数转换为JSON
        //        string jsonData = JsonConvert.SerializeObject(requestData);
        //        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        //        // 添加token到请求头
        //        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        //        // 发送POST请求
        //        HttpResponseMessage response = await httpClient.PostAsync($"{ApiBaseUrl}/web/main/confirmCritical", content);

        //        // 检查响应状态
        //        if (response.IsSuccessStatusCode)
        //        {
        //            string responseBody = await response.Content.ReadAsStringAsync();
        //            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

        //            if (responseObj != null && responseObj.ContainsKey("code") && responseObj["code"].ToString() == "200")
        //            {
        //                // 确认成功，从列表中移除
        //                RemoveProcessedData(value.patientId, value.criticalId, value.itemCode);
        //                MessageBox.Show("危急值确认成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //            }
        //            else
        //            {
        //                MessageBox.Show($"确认失败: {responseObj?["msg"]}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            }
        //        }
        //        else
        //        {
        //            MessageBox.Show($"确认请求失败，状态码: {response.StatusCode}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"确认过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}
        // 3. 添加确认危急值的方法
        private async void ConfirmCriticalValue(CriticalValue value)
        {
            bool checkTimeout = false;
            lock (timeoutActionLock) // 读取服务器设置
            {
                checkTimeout = requiresTimeoutAction;
            }

            //CA签名
            //APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"" + strScene + "\"}", out string strInfo);
            if (APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"1\"}", out string strInfo)) {

                // CA签名后获取Token
                await TimeoutGetAccessTokenAsync(strInfo);
                // --- 如果服务器设置要求检查超时 ---
                if (checkTimeout)
                {
                    Console.WriteLine($"{DateTime.Now}: 服务器设置要求检查超时状态 (criticalId: {value.criticalId})");
                    ApiCriticalValueRow currentStatus = await GetCurrentCriticalValueStatusAsync(value.criticalId);

                    if (currentStatus != null)
                    {
                        // --- 如果已超时 (timeoutCount > 0) ---
                        if (currentStatus.TimeoutCount == 1)
                        {
                            Console.WriteLine($"{DateTime.Now}: 危急值已超时 (TimeoutCount={currentStatus.TimeoutCount})，需要补救措施。");
                            // 弹出提示框
                            var confirmResult = MessageBox.Show("当前危急值已超时，需要填写补救措施。",
                                                                "超时提醒",
                                                                MessageBoxButtons.OK, // 只提供确认按钮
                                                                MessageBoxIcon.Warning);

                            // 不论用户点什么，直接跳转到处理页面
                            Console.WriteLine($"{DateTime.Now}: 用户确认超时提醒，跳转到处理页面。");

                            TimeoutOpenWebPage(value.patientId, value.criticalId); // 打开处理页面
                            MinimizeFormToTray(); // 最小化客户端
                                                  //return; // *** 结束执行，不调用确认API，不移除本地数据 ***
                                                  // 确认成功，从本地列表中移除 (重要：仅在非超时跳转时执行)
                            RemoveProcessedData(value.patientId, value.criticalId, value.itemCode);
                            return;


                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 危急值未超时 (TimeoutCount={currentStatus.TimeoutCount})，执行正常确认流程。");
                            // 未超时，继续执行下面的正常确认流程
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 无法获取危急值当前状态，执行正常确认流程作为后备。");
                        // 获取状态失败，可以决定是阻止确认还是按正常流程处理（这里选择正常流程）
                        MessageBox.Show("获取超时状态失败,请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: 服务器设置不要求检查超时，执行正常确认流程。");
                    // 服务器没要求检查，也执行下面的正常确认流程
                }


                // --- 正常的确认流程 (仅在未超时或服务器不要求检查时执行) ---
                try
                {
                    Console.WriteLine($"{DateTime.Now}: 调用确认API (criticalId: {value.criticalId})...");
                    // 构造请求参数
                    var requestData = new
                    {
                        doctorId = strInfo, // 确保 currentUser 已正确赋值
                        criticalId = value.criticalId,
                        confirmResult = "确认有效" // 或者其他需要的确认结果
                    };
                    // 将参数转换为JSON
                    string jsonData = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    // 添加token到请求头 (确保httpClient和accessToken有效)
                    if (string.IsNullOrEmpty(TimeoutaccessToken))
                    {
                        MessageBox.Show("无法确认：用户未登录或会话已过期。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TimeoutaccessToken);

                    // 发送POST请求
                    HttpResponseMessage response = await httpClient.PostAsync($"{ApiBaseUrl}/web/main/confirmCritical", content); // 确认API地址

                    // 检查响应状态
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                        // 检查业务成功码 (假设也是200)
                        if (responseObj != null && responseObj.ContainsKey("code") && responseObj["code"].ToString() == "200")
                        {
                            Console.WriteLine($"{DateTime.Now}: 危急值确认API调用成功。");
                            // 确认成功，从本地列表中移除 (重要：仅在非超时跳转时执行)
                            RemoveProcessedData(value.patientId, value.criticalId, value.itemCode);
                            // 可选：给用户一个简单的成功提示
                            // MessageBox.Show("危急值确认成功。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            string errorMsg = responseObj != null && responseObj.ContainsKey("msg") ? responseObj["msg"].ToString() : "未知错误";
                            Console.WriteLine($"{DateTime.Now}: 确认API返回业务错误: {errorMsg}");
                            MessageBox.Show($"确认失败: {errorMsg}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 确认API请求失败，状态码: {response.StatusCode}");
                        MessageBox.Show($"确认请求失败，状态码: {response.StatusCode}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        // 考虑Token过期的情况 (401 Unauthorized)
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            // 尝试重新获取Token
                            await GetAccessTokenAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: 确认过程中发生异常: {ex.Message}");
                    MessageBox.Show($"确认过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


            }


        }

        // 新增辅助方法：获取指定危急值的当前状态
        private async Task<ApiCriticalValueRow> GetCurrentCriticalValueStatusAsync(string criticalId)
        {
            if (string.IsNullOrEmpty(criticalId)) return null;

            // 确保已登录且有Token
            if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine($"{DateTime.Now}: 获取危急值状态失败 - 未登录或Token无效。");
                return null;
            }

            string apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=1&criticalId={criticalId}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    Console.WriteLine($"{DateTime.Now}: 正在获取危急值当前状态 (criticalId: {criticalId})...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<CriticalValueApiResponse>(responseBody);

                        // 检查API业务码和返回数据
                        if (apiResponse != null && apiResponse.Code == 200 && apiResponse.Rows != null && apiResponse.Rows.Count > 0)
                        {
                            Console.WriteLine($"{DateTime.Now}: 成功获取到危急值状态。");
                            return apiResponse.Rows[0]; // 返回第一个（理论上应该只有一个）
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 获取危急值状态失败 - API响应无效或未找到数据。 Code: {apiResponse?.Code}, Msg: {apiResponse?.Message}, Rows: {apiResponse?.Rows?.Count}");
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 获取危急值状态失败 - HTTP状态码: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 获取危急值状态时发生异常: {ex.Message}");
                return null;
            }
        }
        // 4. 添加确认并处理危急值的方法

        private async void ConfirmAndProcessCriticalValue(CriticalValue value)
        {


            bool checkTimeout = false;
            lock (timeoutActionLock) // 读取服务器设置
            {
                checkTimeout = requiresTimeoutAction;
            }
            //CA签名
            //APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"" + strScene + "\"}", out string strInfo);
            if (APP_CheckCertificate("{\"LoginUser\":\"" + currentUser.UserId + "\",\"Scene\":\"1\"}", out string strInfo))
            {

                // CA签名后获取Token
                await TimeoutGetAccessTokenAsync(strInfo);
                // --- 如果服务器设置要求检查超时 ---
                if (checkTimeout)
                {
                    Console.WriteLine($"{DateTime.Now}: 服务器设置要求检查超时状态 (criticalId: {value.criticalId})");
                    ApiCriticalValueRow currentStatus = await GetCurrentCriticalValueStatusAsync(value.criticalId);

                    if (currentStatus != null)
                    {
                        // --- 如果已超时 (timeoutCount > 0) ---
                        if (currentStatus.TimeoutCount == 1)
                        {
                            Console.WriteLine($"{DateTime.Now}: 危急值已超时 (TimeoutCount={currentStatus.TimeoutCount})，需要补救措施。");
                            // 弹出提示框
                            var confirmResult = MessageBox.Show("当前危急值已超时，需要填写补救措施。",
                                                                "超时提醒",
                                                                MessageBoxButtons.OK, // 只提供确认按钮
                                                                MessageBoxIcon.Warning);

                            // 不论用户点什么，直接跳转到处理页面
                            Console.WriteLine($"{DateTime.Now}: 用户确认超时提醒，跳转到处理页面。");

                            TimeoutOpenWebPage(value.patientId, value.criticalId); // 打开处理页面

                            MinimizeFormToTray(); // 最小化客户端
                                                  //return; // *** 结束执行，不调用确认API，不移除本地数据 ***
                                                  // 确认成功，从本地列表中移除 (重要：仅在非超时跳转时执行)
                            RemoveProcessedData(value.patientId, value.criticalId, value.itemCode);
                            return; // *** 结束执行，不调用确认API，不移除本地数据 ***
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 危急值未超时 (TimeoutCount={currentStatus.TimeoutCount})，执行正常确认流程。");
                            // 未超时，继续执行下面的正常确认流程
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 无法获取危急值当前状态，执行正常确认流程作为后备。");
                        // 获取状态失败，可以决定是阻止确认还是按正常流程处理（这里选择正常流程）
                        MessageBox.Show("获取超时状态失败,请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: 服务器设置不要求检查超时，执行正常确认流程。");
                    // 服务器没要求检查，也执行下面的正常确认流程

                }



                try
                {
                    // 先确认危急值
                    var requestData = new
                    {
                        doctorId = strInfo,
                        criticalId = value.criticalId,
                        confirmResult = "确认有效"
                    };

                    string jsonData = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");


                    // 添加token到请求头 (确保httpClient和accessToken有效)
                    if (string.IsNullOrEmpty(TimeoutaccessToken))
                    {
                        MessageBox.Show("无法确认处理：用户未登录或会话已过期。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TimeoutaccessToken);

                    HttpResponseMessage response = await httpClient.PostAsync($"{ApiBaseUrl}/web/main/confirmCritical", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);


                        if (responseObj != null && responseObj.ContainsKey("code") && responseObj["code"].ToString() == "200")
                        {
                            // 确认成功，从列表中移除
                            RemoveProcessedData(value.patientId, value.criticalId, value.itemCode);
                            // 确认成功，打开处理页面
                            //OpenWebPage(value.patientId, value.criticalId);

                            TimeoutOpenWebPage(value.patientId, value.criticalId); // 打开处理页面
                            MinimizeFormToTray(); // 处理危急值的时候窗体隐藏到托盘
                        }
                        else
                        {
                            MessageBox.Show($"确认失败: {responseObj?["msg"]}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            //MessageBox.Show("无法确认：用户未登录或会话已过期。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"确认请求失败，状态码: {response.StatusCode}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"确认并处理过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


            }

        }

        private void SetFormPositionToBottomRight()
        {
            // 获取屏幕的工作区域（排除任务栏）
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            // 计算窗体的位置
            int x = workingArea.Right - this.Width;
            int y = workingArea.Bottom - this.Height;

            // 确保窗体位置不超出屏幕
            x = Math.Max(x, workingArea.Left);
            y = Math.Max(y, workingArea.Top);

            // 设置窗体位置
            this.Location = new Point(x, y);
        }

        // 窗体加载事件
        private async void Form1_Load(object sender, EventArgs e)
        {
            //GetDefaultBrowserPath();


            StartHttpServer(); // 启动 HTTP 服务器
            // 窗体加载后自动获取 Token
            //await GetAccessTokenAsync();
            // 由于现在需要等待用户登录，所以不在加载时获取Token
            // 而是在收到登录请求时获取Token
             EnsureFormTopMost(); // 确保窗体置顶
            this.Resize += Form1_Resize;

            //UpdateButtonsVisibility(); // 初始化时更新按钮可见性

            // 初始化标题显示
            UpdateFormTitle(currentUserName, GetUnprocessedCriticalValueCount());


            // 启动时检查更新 (服务器)
            //await CheckForUpdatesAsync(); // 调用新的服务器检查方法 

            // 启动时检查更新 (本地测试版)
            // await CheckForUpdatesLocalAsync(); // 调用新的本地检查方法      

        }
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                Console.WriteLine($"{DateTime.Now}: 正在检查更新...");
                string currentVersionStr = GetCurrentVersion();
                Version currentVersion = new Version(currentVersionStr);
                // 添加 token 到请求头
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await httpClient.GetAsync(UpdateCheckUrl); // httpClient 已在您的代码中定义
                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    //UpdateInfo updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(jsonContent);
                    // 解析 JSON
                    ApiResponse<UpdateInfo> response1 = JsonConvert.DeserializeObject<ApiResponse<UpdateInfo>>(jsonContent);

                    // 获取 data 节点内容
                    UpdateInfo updateInfo = response1.Data;
                    Version latestVersion = new Version(updateInfo.VersionId);

                    if (latestVersion > currentVersion)
                    {
                        Console.WriteLine($"{DateTime.Now}: 发现新版本 {updateInfo.VersionId}。当前版本 {currentVersionStr}。");
                        // 确保在UI线程上操作
                        this.BeginInvoke(new Action(() =>
                        {
                            ShowUpdateNotification(updateInfo);
                        }));
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 当前已是最新版本 ({currentVersionStr})。");
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: 检查更新失败 - HTTP状态码: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 检查更新时发生异常: {ex.Message}");
            }
        }

        //private async Task CheckForUpdatesLocalAsync() // 修改为本地检查逻辑
        //{
        //    try
        //    {
        //        Console.WriteLine($"{DateTime.Now}: 正在从本地检查更新: {UpdateCheckUrl}");
        //        string currentVersionStr = GetCurrentVersion(); // 确保您有 GetCurrentVersion() 方法
        //        Version currentVersion = new Version(currentVersionStr);

        //        if (File.Exists(UpdateCheckUrl))
        //        {
        //            string jsonContent = File.ReadAllText(UpdateCheckUrl);
        //            UpdateInfo updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(jsonContent);

        //            Version latestVersion = new Version(updateInfo.LatestVersion);

        //            if (latestVersion > currentVersion)
        //            {
        //                Console.WriteLine($"{DateTime.Now}: 发现新版本 {updateInfo.LatestVersion}。当前版本 {currentVersionStr}。");
        //                this.BeginInvoke(new Action(() =>
        //                {
        //                    ShowUpdateNotification(updateInfo); // ShowUpdateNotification 方法保持不变
        //                }));
        //            }
        //            else
        //            {
        //                Console.WriteLine($"{DateTime.Now}: 当前已是最新版本 ({currentVersionStr})。");
        //            }
        //        }
        //        else
        //        {
        //            Console.WriteLine($"{DateTime.Now}: 更新清单文件未找到: {UpdateCheckUrl}");
        //            // MessageBox.Show(this, $"更新配置文件 {UpdateCheckUrl} 未找到。", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"{DateTime.Now}: 检查更新时发生异常: {ex.Message}");
        //    }
        //}

        // GetCurrentVersion 方法 (如果还没定义，请添加)
        public static string GetCurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }


        // ShowUpdateNotification 方法 (与之前提供的版本相同，此处不再重复，用于显示提示)
        private void ShowUpdateNotification(UpdateInfo updateInfo)
        {
            string message = $"发现新版本 {updateInfo.VersionId}！\n\n更新内容：\n{updateInfo.ReleaseNotes}\n\n{(updateInfo.IsMandatory == 1 ? "此为强制更新。" : "是否立即更新？")}";
            MessageBoxButtons buttons = updateInfo.IsMandatory == 1 ? MessageBoxButtons.OK : MessageBoxButtons.YesNo;
            DialogResult dialogResult = MessageBox.Show(this, message, "更新提示", buttons, MessageBoxIcon.Information);

            if (updateInfo.IsMandatory == '1' || dialogResult == DialogResult.Yes || dialogResult == DialogResult.OK)
            {

                DownloadAndApplyUpdate(updateInfo); // 调用服务器下载方法
                //DownloadAndApplyUpdateLocal(updateInfo); // 调用新的本地下载方法
            }
        }

        private async void DownloadAndApplyUpdate(UpdateInfo updateInfo)
        {
            bool downloadPackageSuccess = false;
            string downloadPath = ""; // 在try块外部声明，以便finally或catch中可以访问
            try
            {
                // 0. 检查并更新Updater.exe（如果需要）
                // 实际项目中，Updater.exe的版本也应该管理，如果服务器上的Updater版本更高，
                // 则先下载Updater.exe到临时目录，然后用这个新的Updater去执行后续步骤。
                // 为简化，此处假设Updater.exe已存在且版本合适，或包含在主程序更新包中。

                // 1. 下载更新包
               downloadPath = Path.Combine(Path.GetTempPath(), $"CriticalValueSystem_Update_{updateInfo.VersionId}.zip");
                Console.WriteLine($"{DateTime.Now}: 开始下载更新包到 {downloadPath}...");

                // (可以添加一个简单的下载进度窗口)
                ShowDownloadProgressForm();

                using (HttpResponseMessage response = await httpClient.GetAsync($"{ApiBaseUrl}{updateInfo.DownloadUrl}", HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                                  fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalBytes = response.Content.Headers.ContentLength;
                        var canReportProgress = totalBytes.HasValue;
                        var totalBytesRead = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;
                        int lastReportedProgress = -1; // 用于避免过于频繁地更新UI
                        do
                        {
                            var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                isMoreToRead = false;
                                continue;
                            }
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            if (canReportProgress && totalBytes.Value > 0)
                            {
                                int currentProgress = (int)(totalBytesRead * 100 / totalBytes.Value);
                                //UpdateDownloadProgress((int)(totalBytesRead * 100 / totalBytes.Value));
                                //Console.WriteLine($"下载进度: {(int)(totalBytesRead * 100 / totalBytes.Value)}%");

                                if (currentProgress != lastReportedProgress)
                                {
                                    UpdateDownloadProgress(currentProgress); // 更新进度
                                    Console.WriteLine($"下载进度: {currentProgress}%");
                                    lastReportedProgress = currentProgress;
                                }


                            }
                            else
                            {
                                UpdateDownloadProgress(-1, $"已下载 {totalBytesRead / 1024} KB"); // 不确定进度
                            }



                        }
                        while (isMoreToRead);
                    }
                }
                UpdateDownloadProgress(100, "更新包下载完成！"); // 下载完成
                Console.WriteLine($"{DateTime.Now}: 更新包下载完成。");
                downloadPackageSuccess = true; // 标记主包下载成功

                // (可选) 校验Checksum
                // if (!VerifyChecksum(downloadPath, updateInfo.Checksum)) { Console.WriteLine("校验和不匹配！"); return; }


                // 2. 准备启动Updater.exe
                string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UpdaterExecutableName);
                if (!File.Exists(updaterPath))
                {
                    Console.WriteLine($"{DateTime.Now}: {UpdaterExecutableName} 未找到，尝试从服务器下载...");
                    UpdateDownloadProgress(0, $"正在下载更新器 {UpdaterExecutableName}..."); // 为下载Updater显示进度/状态

                    // **重要**: updateInfo.DownloadUrl 这里很可能是错误的，应该是 updateInfo.UpdaterDownloadUrl 或类似的特定于更新器的URL
                    // 假设您有一个正确的URL，例如：string updaterDownloadUrl = updateInfo.GetUpdaterUrl();
                    string actualUpdaterDownloadUrl = $"{ApiBaseUrl}/path/to/Updater.exe"; // 这里需要正确的Updater下载地址
                                                                                           // 例如，如果UpdateInfo有UpdaterDownloadUrl属性: actualUpdaterDownloadUrl = updateInfo.UpdaterDownloadUrl; (确保它是完整或相对正确的)
                                                                                           // if (Uri.IsWellFormedUriString(updateInfo.UpdaterDownloadUrl, UriKind.Absolute)) actualUpdaterDownloadUrl = updateInfo.UpdaterDownloadUrl;
                                                                                           // else actualUpdaterDownloadUrl = new Uri(new Uri(ApiBaseUrl), updateInfo.UpdaterDownloadUrl).ToString();

                    // 为简化，假设您能获取到正确的 updaterDownloadUrl
                    // 注意：下面的 httpClient.GetAsync(updateInfo.DownloadUrl) 应该是针对Updater的URL
                    // 比如： httpClient.GetAsync(actualUpdaterDownloadUrl)

                    updaterPath = Path.Combine(Path.GetTempPath(), UpdaterExecutableName); // 下载到临时目录
                    using (HttpResponseMessage updaterResponse = await httpClient.GetAsync(actualUpdaterDownloadUrl /* 这里应为Updater的真实下载地址 */))
                    {
                        updaterResponse.EnsureSuccessStatusCode();
                        using (FileStream fs = new FileStream(updaterPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await updaterResponse.Content.CopyToAsync(fs); // 下载Updater时没有实现进度条
                        }
                    }
                    UpdateDownloadProgress(100, "更新器下载完成！"); // 更新器下载完成
                    Console.WriteLine($"{DateTime.Now}: {UpdaterExecutableName} 下载完成到 {updaterPath}。");
                }
                // 如果前面的下载都顺利，进度窗体仍然开着，短暂显示完成信息
                await Task.Delay(1000); // 让用户看到 "下载完成" 或 "更新器下载完成"
                CloseDownloadProgressForm(); // <<< 在启动Updater之前，关闭进度窗体


                // 3. 启动Updater.exe并退出主程序
                string mainAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string mainAppDir = AppDomain.CurrentDomain.BaseDirectory;
                string effectiveMainAppDir = mainAppDir.EndsWith("\\") ? mainAppDir.Substring(0, mainAppDir.Length - 1) : mainAppDir;

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{downloadPath}\" \"{effectiveMainAppDir}\" \"{Path.GetFileName(mainAppPath)}\"",
                    UseShellExecute = true, // 使用ShellExecute以方便请求管理员权限（如果需要）
                    Verb = "runas" // 尝试请求管理员权限，如果Updater需要写保护目录
                };

                Console.WriteLine($"{DateTime.Now}: 启动更新程序: {updaterPath}");
                Console.WriteLine($"{DateTime.Now}: 参数: {startInfo.Arguments}");

                try
                {
                    Process.Start(startInfo);
                    Application.Exit(); // 关闭当前应用程序
                }
                catch (System.ComponentModel.Win32Exception ex) // 用户取消UAC等
                {
                    Console.WriteLine($"{DateTime.Now}: 启动更新程序失败 (可能是用户取消了UAC): {ex.Message}");
                    MessageBox.Show(this, "启动更新程序失败，请检查权限或稍后重试。", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //File.Delete(downloadPath); // 清理已下载的更新包
                    if (downloadPackageSuccess && File.Exists(downloadPath)) File.Delete(downloadPath);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 下载或应用更新时发生错误: {ex.Message}");
                UpdateDownloadProgress(0, $"错误: {ex.Message}"); // 在进度窗体上显示错误信息
                await Task.Delay(2000); // 给用户一点时间看错误信息
                CloseDownloadProgressForm(); // <<< 在最外层的catch块中，确保任何异常都关闭进度窗体
                MessageBox.Show(this, $"更新过程中发生错误: {ex.Message}", "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (downloadPackageSuccess && File.Exists(downloadPath)) // 只有当主包下载（部分）成功时才尝试删除
                {
                    try { File.Delete(downloadPath); } catch { /* 忽略删除失败 */ }
                }
            }
        }

        // 更新进度的方法 (这就是您之前注释掉的方法之一)
        private void UpdateDownloadProgress(int percentage, string statusMessage = null)
        {
            if (_downloadProgressForm != null && !_downloadProgressForm.IsDisposed)
            {
                _downloadProgressForm.UpdateProgress(percentage);
                if (statusMessage != null)
                {
                    _downloadProgressForm.SetStatus(statusMessage);
                }
            }
        }
        // 关闭进度窗体的方法
        private void CloseDownloadProgressForm()
        {
            if (_downloadProgressForm != null && !_downloadProgressForm.IsDisposed)
            {
                _downloadProgressForm.Close();
                _downloadProgressForm = null; // 释放引用
            }
        }
        // 显示进度窗体的方法
        private void ShowDownloadProgressForm(string initialStatus = "正在准备下载...")
        {
            if (_downloadProgressForm != null && !_downloadProgressForm.IsDisposed)
            {
                _downloadProgressForm.Close(); // 如果已存在，先关闭旧的
            }
            _downloadProgressForm = new DownloadProgressForm();
            _downloadProgressForm.SetStatus(initialStatus);
            _downloadProgressForm.UpdateProgress(0); // 初始进度为0
            _downloadProgressForm.Show(this); // 将 Form1 作为其所有者，非模态显示
            Application.DoEvents(); // 确保窗体立即绘制出来
        }

        //// ShowUpdateNotification 方法 (与之前提供的版本相同，此处不再重复，用于显示提示)
        //private void ShowUpdateNotification(UpdateInfo updateInfo)
        //{
        //    string message = $"发现新版本 {updateInfo.LatestVersion}！\n\n更新内容：\n{updateInfo.ReleaseNotes}\n\n{(updateInfo.IsMandatory ? "此为强制更新。" : "是否立即更新？")}";
        //    MessageBoxButtons buttons = updateInfo.IsMandatory ? MessageBoxButtons.OK : MessageBoxButtons.YesNo;
        //    DialogResult dialogResult = MessageBox.Show(this, message, "更新提示", buttons, MessageBoxIcon.Information);

        //    if (updateInfo.IsMandatory || dialogResult == DialogResult.Yes || dialogResult == DialogResult.OK)
        //    {
        //        DownloadAndApplyUpdateLocal(updateInfo); // 调用新的本地下载方法
        //    }
        //}



        //private async void DownloadAndApplyUpdateLocal(UpdateInfo updateInfo) // 修改为本地下载/复制逻辑
        //{
        //    string tempUpdatePackagePath = Path.Combine(Path.GetTempPath(), $"CriticalValueSystem_Update_{updateInfo.LatestVersion}.zip");
        //    string localUpdaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UpdaterExecutableName); // 默认更新器在程序目录下

        //    try
        //    {
        //        Console.WriteLine($"{DateTime.Now}: 准备本地更新...");

        //        // 1. "下载" (复制) 更新包
        //        if (File.Exists(updateInfo.DownloadUrl))
        //        {
        //            File.Copy(updateInfo.DownloadUrl, tempUpdatePackagePath, true);
        //            Console.WriteLine($"{DateTime.Now}: 更新包已从本地复制到 {tempUpdatePackagePath}");
        //        }
        //        else
        //        {
        //            Console.WriteLine($"{DateTime.Now}: 错误: 本地更新包未找到于 {updateInfo.DownloadUrl}");
        //            MessageBox.Show(this, $"本地更新包未找到: {updateInfo.DownloadUrl}", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            return;
        //        }

        //        // 2. 检查/准备 Updater.exe
        //        if (!File.Exists(localUpdaterPath)) // 如果主程序目录没有Updater.exe
        //        {
        //            Console.WriteLine($"{DateTime.Now}: {UpdaterExecutableName} 在主目录未找到，尝试从本地源复制...");
        //            if (!string.IsNullOrEmpty(updateInfo.UpdaterDownloadUrl) && File.Exists(updateInfo.UpdaterDownloadUrl))
        //            {
        //                // 为简单起见，本地测试时，我们将Updater复制到临时目录并从那里运行
        //                // 或者，更常见的做法是确保Updater.exe始终存在于主程序目录，或随主包一起更新。
        //                // 这里我们假设会从 updateInfo.UpdaterDownloadUrl 复制到主程序目录（如果需要权限，可能会失败）
        //                // 为安全和简单，测试时建议将Updater.exe直接放在主程序运行目录下
        //                localUpdaterPath = Path.Combine(Path.GetTempPath(), UpdaterExecutableName); // 复制到临时目录运行
        //                File.Copy(updateInfo.UpdaterDownloadUrl, localUpdaterPath, true);
        //                Console.WriteLine($"{DateTime.Now}: {UpdaterExecutableName} 已从本地复制到 {localUpdaterPath}。");
        //            }
        //            else
        //            {
        //                Console.WriteLine($"{DateTime.Now}: 错误: 本地 {UpdaterExecutableName} 源路径无效或文件不存在: {updateInfo.UpdaterDownloadUrl}");
        //                MessageBox.Show(this, $"本地更新器源文件未找到: {updateInfo.UpdaterDownloadUrl}", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                File.Delete(tempUpdatePackagePath); // 清理已复制的主更新包
        //                return;
        //            }
        //        }
        //        // 如果localUpdaterPath仍然指向AppDomain.CurrentDomain.BaseDirectory下的Updater.exe，确保它存在
        //        else if (!File.Exists(localUpdaterPath))
        //        {
        //            Console.WriteLine($"{DateTime.Now}: 错误: 更新器 {localUpdaterPath} 未找到。");
        //            MessageBox.Show(this, $"更新器 {localUpdaterPath} 未找到。", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            File.Delete(tempUpdatePackagePath); // 清理已复制的主更新包
        //            return;
        //        }


        //        // 3. 启动Updater.exe并退出主程序
        //        //string mainAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //        //string mainAppDir = AppDomain.CurrentDomain.BaseDirectory;



        //        // ... 在 ProcessStartInfo 构造之前 ...
        //        string mainAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //        string mainAppDir = AppDomain.CurrentDomain.BaseDirectory;
        //        string mainAppExeName = Path.GetFileName(mainAppPath); // 获取主程序exe名


        //        // ---- START MODIFICATION ----
        //        // 如果 mainAppDir 以 "\" 结尾，则移除它，以防止转义参数字符串中的结束引号
        //        string effectiveMainAppDir = mainAppDir.EndsWith("\\") ? mainAppDir.Substring(0, mainAppDir.Length - 1) : mainAppDir;
        //        // ---- END MODIFICATION ----

        //        Console.WriteLine($"[DEBUG] tempUpdatePackagePath: '{tempUpdatePackagePath}'");
        //        Console.WriteLine($"[DEBUG] mainAppDir (original): '{mainAppDir}'");
        //        Console.WriteLine($"[DEBUG] effectiveMainAppDir (for argument): '{effectiveMainAppDir}'"); // 新增日志
        //        Console.WriteLine($"[DEBUG] mainAppExeName: '{mainAppExeName}'");
        //        Console.WriteLine($"[DEBUG] localUpdaterPath (FileName for ProcessStartInfo): '{localUpdaterPath}'");


        //        ProcessStartInfo startInfo = new ProcessStartInfo
        //        {
        //            FileName = localUpdaterPath, // 使用找到或复制的Updater路径
        //            Arguments = $"\"{tempUpdatePackagePath}\" \"{effectiveMainAppDir}\" \"{Path.GetFileName(mainAppPath)}\"",
        //            UseShellExecute = true,
        //            // Verb = "runas" // 如果需要提权，取消注释，但本地测试时尽量避免复杂化
        //        };

        //        Console.WriteLine($"{DateTime.Now}: 启动更新程序: {startInfo.FileName}");
        //        Console.WriteLine($"{DateTime.Now}: 参数: {startInfo.Arguments}");

        //        try
        //        {
        //            Process.Start(startInfo);
        //            Application.Exit();
        //        }
        //        catch (System.ComponentModel.Win32Exception ex)
        //        {
        //            Console.WriteLine($"{DateTime.Now}: 启动更新程序失败 (可能是用户取消了UAC或文件问题): {ex.Message}");
        //            MessageBox.Show(this, $"启动更新程序失败: {ex.Message}", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            if (File.Exists(tempUpdatePackagePath)) File.Delete(tempUpdatePackagePath);
        //            // 如果updater是从临时目录启动的，考虑也删除它
        //            if (localUpdaterPath != Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UpdaterExecutableName) && File.Exists(localUpdaterPath))
        //            {
        //                // File.Delete(localUpdaterPath); // 这可能因为Updater正在运行而失败
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"{DateTime.Now}: 应用本地更新时发生错误: {ex.Message}");
        //        MessageBox.Show(this, $"更新过程中发生错误: {ex.Message}", "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        if (File.Exists(tempUpdatePackagePath)) File.Delete(tempUpdatePackagePath);
        //    }
        //}

        private void StartHttpServer()
        {
            httpListener = new HttpListener();
            //针对多个IP，只取有默认网关的
           // string[] ipAddresses = GetLocalIPAddresses();
            string ipAddresses = GetLocalIP();
            //foreach (string ip in ipAddresses)
            //{
            //    httpListener.Prefixes.Add($"http://{ip}:9980/");
            //}
            try
            {
                // 添加localhost监听
                httpListener.Prefixes.Add("http://localhost:9980/");
                // 添加127.0.0.1监听
                httpListener.Prefixes.Add("http://127.0.0.1:9980/");
                // 添加实际IP地址监听
                // httpListener.Prefixes.Add($"http://{ipAddresses}:9980/");
                //针对单个
                if (!string.IsNullOrEmpty(ipAddresses))
                {
                    httpListener.Prefixes.Add($"http://{ipAddresses}:9980/");
                }

                //  //针对多个 添加所有有默认网关的本地IP地址的监听
                //if (ipAddresses.Length > 0)
                //{
                //    foreach (string ip in ipAddresses)
                //    {
                //        httpListener.Prefixes.Add($"http://{ip}:9980/");
                //    }
                //}
                //else
                //{
                //    // 如果没有找到有默认网关的IP地址，可以记录日志或显示警告
                //    Console.WriteLine("警告：未找到具有默认网关的IP地址");
                //}


                //httpListener.Start();
                //Task.Run(() => ListenForRequests());

                // 如果希望接受所有IP的请求，可以添加以下前缀（可选）
                // httpListener.Prefixes.Add("http://*:9980/");
                // 注意：使用"*"需要管理员权限，且不能与特定IP同时使用

                try
                {
                    httpListener.Start();
                    Task.Run(() => ListenForRequests());
                }
                catch (HttpListenerException ex)
                {
                    MessageBox.Show($"启动HTTP服务器失败: {ex.Message}\n可能需要以管理员身份运行应用程序。",
                                   "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动HTTP服务器时发生异常: {ex.Message}",
                                   "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }

            }
            catch (Exception ex)
            {
             Application.Exit();

            }

        }

        //private string[] GetLocalIPAddresses()
        //{
        //    return Dns.GetHostEntry(Dns.GetHostName())
        //        .AddressList
        //        .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
        //        .Select(ip => ip.ToString())
        //        .ToArray();
        //}
        private string[] GetLocalIPAddresses()
        {
            // 需要添加引用: using System.Net.NetworkInformation;
            List<string> ipAddressesWithGateway = new List<string>();

            // 获取所有网络接口
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in adapters)
            {
                // 只考虑运行中的接口，排除虚拟接口等
                if (adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    // 获取该接口的IP属性
                    IPInterfaceProperties ipProperties = adapter.GetIPProperties();

                    // 检查是否有默认网关
                    if (ipProperties.GatewayAddresses.Count > 0)
                    {
                        // 获取该接口的所有单播地址
                        foreach (UnicastIPAddressInformation ipInfo in ipProperties.UnicastAddresses)
                        {
                            // 只添加IPv4地址
                            if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipAddressesWithGateway.Add(ipInfo.Address.ToString());
                            }
                        }
                    }
                }
            }

            return ipAddressesWithGateway.ToArray();
        }

        // 监听 HTTP 请求
        private async void ListenForRequests()
        {
            while (httpListener.IsListening)
            {
                try
                {
                    var context = await httpListener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    //AppendTextToResult($"HTTP 服务器异常：{ex.Message}\r\n");
                }
            }
        }

        // 处理 HTTP 请求
        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/criticalValue")
                {
                    // 处理危急值数据
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestBody = reader.ReadToEnd();
                        var values = JsonConvert.DeserializeObject<List<CriticalValue>>(requestBody);
                        Console.WriteLine(requestBody);
                        if (values != null && values.Any())
                        {
                        
                            ProcessCriticalValueData(values);
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.ContentType = "application/json";
                            //string responseBody = JsonConvert.SerializeObject(new { success = true });
                            string responseBody = "true";
                            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/criticalConfirm")
                {


                    // 处理已处理数据
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestBody = reader.ReadToEnd();
                        var processedData = JsonConvert.DeserializeObject<List<ProcessedData>>(requestBody);
                        //var processedData = JsonConvert.DeserializeObject<ProcessedData>(requestBody);
                        //var values = JsonConvert.DeserializeObject<List<CriticalValue>>(requestBody);
                        //if (processedData != null &&
                        //                   !string.IsNullOrEmpty(processedData.patientId) &&
                        //                   !string.IsNullOrEmpty(processedData.exReportId) &&
                        //                   !string.IsNullOrEmpty(processedData.itemCode))


                        // 3. 遍历排序后的 List，添加项到 ListBox
                        foreach (var value in processedData)
                        {
                            if (processedData != null &&
                                !string.IsNullOrEmpty(value.CriticalId))
                            {

                                // 首先关闭对应的浏览器
                                CloseBrowser(value.patientId, value.CriticalId);

                                RemoveProcessedData(value.patientId, value.CriticalId, value.itemCode);
                                context.Response.StatusCode = (int)HttpStatusCode.OK;
                                context.Response.ContentType = "application/json";
                                //string responseBody = JsonConvert.SerializeObject(new { success = true });
                                string responseBody = "true";
                                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            }
                            else
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }


                        //if (processedData != null &&                            
                        //    !string.IsNullOrEmpty(processedData.CriticalId) )
                        //{

                        //    // 首先关闭对应的浏览器
                        //    CloseBrowser(processedData.patientId, processedData.CriticalId);

                        //    RemoveProcessedData(processedData.patientId, processedData.CriticalId, processedData.itemCode);
                        //    context.Response.StatusCode = (int)HttpStatusCode.OK;
                        //    context.Response.ContentType = "application/json";
                        //    //string responseBody = JsonConvert.SerializeObject(new { success = true });
                        //    string responseBody = "true";
                        //    byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                        //    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        //}
                        //else
                        //{
                        //    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        //}
                    }
                }
                // 新增：单点登录接口
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/login")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestBody = reader.ReadToEnd();
                        var loginInfo = JsonConvert.DeserializeObject<UserLoginInfo>(requestBody);

                        if (loginInfo != null &&
                            !string.IsNullOrEmpty(loginInfo.UserId) &&
                            !string.IsNullOrEmpty(loginInfo.DeptId))
                        {
                            // 保存用户信息
                            currentUser = loginInfo;
                            isUserLoggedIn = true;

                            // 使用委托异步更新UI
                            this.BeginInvoke(new Action(() => {
                                try
                                {
                                    // 更新标题，包括用户名和危急值数量
                                    UpdateFormTitle(loginInfo.UserName, GetUnprocessedCriticalValueCount());

                                    // 确保窗体置顶
                                    this.TopMost = true;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"更新UI时发生错误: {ex.Message}");
                                }
                            }));

                            // 开始获取Token过程
                            Task.Run(async () =>
                            {
                                //await GetAccessTokenAsync();
                                //loginWaitHandle.Set(); // 通知登录完成
                                bool tokenAcquired = await GetAccessTokenAsync();
                                if (tokenAcquired)
                                {
                                    Console.WriteLine($"{DateTime.Now}: Token acquired successfully after login.");

                                    // 登录时检查更新 (服务器)
                                   // await CheckForUpdatesAsync(); // 调用新的服务器检查方法 
                                    // **** 新增：获取按钮可见性设置 ****
                                    await FetchButtonVisibilitySettingAsync();
                                    // **** 新增：获取补救措施 ****
                                    await FetchTimeoutActionSettingAsync(); // <-- 新增调用

                                    // **** 新增：获取医技CA开启 ****
                                    await FetchEnabledCaSettingAsync(); // <-- 新增调用

                                    // **** 获取初始数据和数量 ****
                                    // 1. 获取未确认列表（会更新本地列表）
                                    await FetchUnconfirmedCriticalValuesAsync(); // 这个方法内部会调用 Reconcile...


                                    // 2. 获取待处理数量 (在Reconcile之后调用，确保本地列表更新完再取待处理数)
                                    // （或者将获取待处理数逻辑移到 ReconcileAndProcessPolledData 方法末尾）
                                    // int initialProcessingCount = await FetchPendingProcessingCountAsync();

                                    // **** 更新标题 ****
                                    // (现在 ReconcileAndProcessPolledData 会负责更新标题)
                                    // UpdateFormTitle(currentUser.UserName, GetUnprocessedCriticalValueCount(), initialProcessingCount >= 0 ? initialProcessingCount : (int?)null);


                                    // Start the periodic polling timer *after* the initial fetch
                                    if (pollingTimer != null)
                                    {
                                        pollingTimer.Enabled = true;
                                        Console.WriteLine($"{DateTime.Now}: Polling timer started.");
                                    }
                                    // Start the heartbeat timer (if not already handled elsewhere after token acquisition)
                                    if (heartbeatTimer != null && !heartbeatTimer.Enabled)
                                    {
                                        heartbeatTimer.Enabled = true;
                                        Console.WriteLine($"{DateTime.Now}: Heartbeat timer started.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"{DateTime.Now}: Failed to acquire token after login.");
                                    // Handle token acquisition failure (e.g., notify user, retry logic?)
                                }
                                loginWaitHandle.Set(); // Signal login process completion
                            });

                            SendSuccessResponse(context);
                        }
                        else
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                // AppendTextToResult($"处理请求异常：{ex.Message}\r\n");

                // 创建一个标准化的错误响应
                var errorResponse = new
                {
                    Message = "An unexpected error occurred. Please try again later.",
                    ErrorCode = (int)HttpStatusCode.InternalServerError,  // 可以根据需要自定义错误代码
                    ExceptionMessage = ex.Message,  // 仅返回简洁的错误消息
                    // StackTrace = ex.StackTrace // 可以根据需求返回或隐藏堆栈信息
                    //你可以选择是否返回堆栈信息。如果安全性要求较高，通常会避免返回堆栈信息，只返回一个通用的错误信息。
                };


                // 将错误信息以 JSON 格式返回
                context.Response.ContentType = "application/json";
                string responseBody = JsonConvert.SerializeObject(errorResponse);
                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);


            }
            finally
            {
                //  context.Response.Close();
                if (context.Response != null)
                {
                    try { context.Response.Close(); } catch { } // Ensure response is closed
                }
            }
        }


        // 发送成功响应
        private void SendSuccessResponse(HttpListenerContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            string responseBody = JsonConvert.SerializeObject(new { success = "true" });
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        // Modify form closing event to stop the timer
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                // Do not stop timers here if hiding to tray
            }
            else // Handle application exit (e.g., from tray menu or system shutdown)
            {
                // Stop timers gracefully
                if (heartbeatTimer != null)
                {
                    heartbeatTimer.Stop();
                    heartbeatTimer.Dispose();
                }
                if (pollingTimer != null)
                {
                    pollingTimer.Stop();
                    pollingTimer.Dispose();
                }
                // Dispose other resources like HttpClient, HttpListener if necessary
                httpListener?.Stop();
                httpListener?.Close();
                httpClient?.Dispose();
                trayIcon?.Dispose();

            }
            base.OnFormClosing(e);
        }

        // 获取未处理危急值数量的方法
        private int GetUnprocessedCriticalValueCount()
        {
            lock (criticalValuesLock)
            {
                return criticalValues.Count;
            }
        }
        // 更新窗体标题
        private void UpdateFormTitle()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateFormTitle));
                return;
            }

            if (!string.IsNullOrEmpty(currentUser.UserName))
            {
                this.Text = $"危急值客户端 - 登录人: {currentUser.UserName}";
            }
            else
            {
                this.Text = "危急值客户端";
            }
        }
        //移除列表中已处理的数据
        private async  void RemoveProcessedData(string patientId, string reportId, string itemCode)
        {
            bool removed = false;
            int remainingCount = 0;
            lock (criticalValuesLock)
            {
                var valueToRemove = criticalValues.FirstOrDefault(v =>
                  // v.patientId == patientId &&
                  //  v.exReportId == reportId &&
                  //  v.itemCode == itemCode
                  v.criticalId == reportId
                );

                if (valueToRemove != null)
                {
                    removed = criticalValues.Remove(valueToRemove);
                }
                remainingCount = criticalValues.Count;
            }

            if (removed)
            {
                //UpdateButtonsVisibility(); // 移除数据后更新按钮可见性


                // 更新按钮可见性（现在会考虑列表是否为空）
                ApplyCurrentButtonVisibilitySetting(); // <--- 调用更新
                // 更新标题显示剩余的危急值数量
                // UpdateFormTitle(null, remainingCount);
                // **** 新增：重新获取待处理数量并更新标题 ****
                int latestProcessingCount = await FetchPendingProcessingCountAsync();
                UpdateFormTitle(null, remainingCount, latestProcessingCount >= 0 ? latestProcessingCount : (int?)null);

                UpdateCriticalValueList();
                UpdateTrayIconText();
                ShowAndFocusForm();//服务端处理完数据重新显示窗体
                if (criticalValues.Count == 0)
                {
                    StopBlinking();
                    MinimizeFormToTray();
                }
            }
        }


        //最小化到托盘
        private void MinimizeFormToTray()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(MinimizeFormToTray));
            }
            else
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            }
        }

        // 初始化托盘图标
        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                //Icon = SystemIcons.Application, // 托盘图标
                Icon = Properties.Resources.weijizhi,
                Text = "危急值客户端", // 托盘提示文本
                Visible = true // 显示托盘图标
            };

            // 托盘图标双击事件
            trayIcon.DoubleClick += (sender, e) =>
            {
                this.Show(); // 显示窗口
                this.WindowState = FormWindowState.Normal; // 恢复窗口状态
            };

            // 托盘右键菜单
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("退出", null, (sender, e) => Application.Exit());
            trayIcon.ContextMenuStrip = trayMenu;
        }
        // 初始化定时器
        private void InitializeTimer()
        {
            heartbeatTimer = new System.Timers.Timer(30000); // 30 秒
            heartbeatTimer.Elapsed += async (sender, e) => await SendHeartbeatAsync();
            heartbeatTimer.AutoReset = true; // 设置为重复执行
            heartbeatTimer.Enabled = false; // 初始状态为禁用
        }
        // New method to initialize the polling timer
        //查询未确认的定时器
        private void InitializePollingTimer()
        {
            pollingTimer = new System.Timers.Timer(70000); // 60 seconds interval
            //pollingTimer.Elapsed += async (sender, e) =>
            //{
            //    // 同时轮询危急值和设置
            //    await PollUnconfirmedCriticalValuesAsync();//获取待确认
            //    await FetchButtonVisibilitySettingAsync();按钮可见性
            //    await FetchTimeoutActionSettingAsync(); // <-- 新增调用 超时补救设置
            //};
            pollingTimer.Elapsed += async (sender, e) => await FetchUnconfirmedCriticalValuesAsync();
            pollingTimer.AutoReset = true; // Keep polling
            pollingTimer.Enabled = false; // Start disabled
        }

        // 新增一个方法用于处理轮询返回的数据并同步本地列表
        private async void ReconcileAndProcessPolledData(List<CriticalValue> serverUnconfirmedValues)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ReconcileAndProcessPolledData(serverUnconfirmedValues)));
                return;
            }

            bool listChanged = false;

            // 1. 将服务器返回的待确认ID存入HashSet，方便快速查找
            var serverUnconfirmedIds = new HashSet<string>(serverUnconfirmedValues.Select(v => v.criticalId));

            lock (criticalValuesLock) // 确保线程安全地操作共享列表
            {
                // 2. 找出并移除本地存在但服务器上已不再是待确认的项
                // 使用 RemoveWhere 可以高效地移除满足条件的元素
                int removedCount = criticalValues.RemoveWhere(localValue =>
                    !string.IsNullOrEmpty(localValue.criticalId) && // 确保本地项有ID
                    !serverUnconfirmedIds.Contains(localValue.criticalId) // 如果服务器最新列表不包含此ID，则移除
                );

                if (removedCount > 0)
                {
                    listChanged = true;
                    Console.WriteLine($"{DateTime.Now}: Removed {removedCount} items confirmed by others.");
                }

                // 3. 添加服务器返回列表中新增的待确认项
                foreach (var serverValue in serverUnconfirmedValues)
                {
                    // criticalValues 是 HashSet，Add 方法如果元素已存在会返回 false
                    if (criticalValues.Add(serverValue))
                    {
                        listChanged = true; // 如果成功添加了新项，标记列表已改变
                        Console.WriteLine($"{DateTime.Now}: Added new unconfirmed item: {serverValue.criticalId}");
                    }
                }
            } // 释放锁

            // 4. 如果列表有任何变化（移除或添加），则更新UI
            if (listChanged)
            {
                Console.WriteLine($"{DateTime.Now}: Critical value list changed, updating UI.");
                UpdateCriticalValueList(); // 更新ListBox显示
                UpdateTrayIconText();      // 更新托盘图标文本
                //UpdateButtonsVisibility(); // 更新按钮可见性

                // 更新按钮可见性（现在会考虑列表是否为空）
                ApplyCurrentButtonVisibilitySetting(); // <--- 调用更新

                //UpdateFormTitle(null, GetUnprocessedCriticalValueCount()); // 更新标题

                // 如果添加了新项或列表非空，可能需要重新开始闪烁和显示窗口
                if (criticalValues.Count > 0)
                {
                    StartBlinking();
                    // 决定是否总是弹出窗口，或者只在有新项添加时弹出
                    ShowAndFocusForm();
                }
                else
                {
                    // 如果列表变空了，停止闪烁并可能最小化
                    StopBlinking();
                    MinimizeFormToTray();
                }
            }
            else
            {
                Console.WriteLine($"{DateTime.Now}: No changes detected in critical values after polling.");
            }

            // **** 新增：无论列表是否变化，都获取最新的待处理数量并更新标题 ****
            int latestProcessingCount = await FetchPendingProcessingCountAsync();
            // 使用当前最新的本地未确认数 和 刚获取的待处理数 来更新标题
            UpdateFormTitle(null, GetUnprocessedCriticalValueCount(), latestProcessingCount >= 0 ? latestProcessingCount : (int?)null);
            // 如果latestProcessingCount为-1（获取失败），则传递null，UpdateFormTitle会使用上次的值

        }

        // New method to fetch unconfirmed data from the server
        // 修改 FetchUnconfirmedCriticalValuesAsync 方法
        private async Task FetchUnconfirmedCriticalValuesAsync()
        {
            // Prevent concurrent execution
            lock (pollingLock)
            {
                if (isPolling)
                {
                    Console.WriteLine($"{DateTime.Now}: Polling already in progress, skipping.");
                    return;
                }
                isPolling = true;
            }
            string apiUrl;
            try
            {
                // Ensure user is logged in and token is valid
                if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken) || DateTime.Now >= tokenExpirationTime)
                {
                    Console.WriteLine($"{DateTime.Now}: Polling skipped: User not logged in or token invalid/expired.");
                    return;
                }
                if (currentUser.Nature.Equals("1"))//医生
                {

                    if (ClientType.Equals("1"))
                    {
                        apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=100&confirmStatus=待确认&creatorId={currentUser.UserId}"; // 门诊查本人

                    }
                    else
                    {
                        apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=100&confirmStatus=待确认&deptId={currentUser.DeptId}"; // 住院查科室

                    }


                }
                else if (currentUser.Nature.Equals("2"))//护士
                {
                    apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=100&confirmStatus=待确认&wardId={currentUser.DeptId}"; // 护士查科室
                }
                else 
                {
                    //医技获取已处理未查看的数据
                    apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=1000&confirmStatus=已确认"; // 已处理未查看的数据
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    Console.WriteLine($"{DateTime.Now}: Polling for unconfirmed critical values for user {currentUser.UserId}...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<CriticalValueApiResponse>(responseBody);

                        if (apiResponse != null && apiResponse.Code == 200 && apiResponse.Rows != null)
                        {
                            Console.WriteLine($"{DateTime.Now}: Received {apiResponse.Rows.Count} unconfirmed items from server.");

                            // Map API response rows to our CriticalValue model
                            List<CriticalValue> fetchedValues = apiResponse.Rows.Select(row => new CriticalValue
                            {
                                PatientName = row.PatientName,
                                Indicator = row.ItemName,
                                Value = row.CriticalValueString,
                                patientId = row.PatientId,
                                criticalId = row.CriticalId,
                                deptName = row.DeptName,
                                triggerTime = row.TriggerTime,
                                sourceSystem=row.sourceSystem,
                                Status = row.ConfirmStatus,
                                itemCode = row.ReportId, // Assuming ReportId maps to itemCode for uniqueness/processing logic
                                IsProcessed = false
                            }).ToList();

                            // **修改点：调用新的同步方法，而不是 ProcessCriticalValueData**
                          ReconcileAndProcessPolledData(fetchedValues);
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: Polling API request successful but response indicates failure or no data. Code: {apiResponse?.Code}, Msg: {apiResponse?.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: Polling API request failed. Status Code: {response.StatusCode}");
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            Console.WriteLine($"{DateTime.Now}: Token likely expired during poll. Attempting refresh...");
                            await GetAccessTokenAsync();
                        }
                        // 可以考虑增加重试逻辑或更详细的错误处理
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: Error during polling: {ex.Message}");
                // 记录详细错误日志
            }
            finally
            {
                lock (pollingLock)
                {
                    isPolling = false; // Release the lock
                }
            }
        }

        // 新增方法：从服务器获取待处理危急值数量
        private async Task<int> FetchPendingProcessingCountAsync()
        {
            // 确保已登录且有Token
            if (!isUserLoggedIn || string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine($"{DateTime.Now}: 获取待处理数量失败 - 未登录或Token无效。");
                return -1; // 返回-1表示获取失败
            }

            // 构建API URL，查询状态为“待处理”且创建者为当前用户的数据
            string apiUrl = $"{ApiBaseUrl}/critical/value/list?pageNum=1&pageSize=100&confirmStatus=已确认&status=待处理&confirmDoctorId={currentUser.UserId}";
            // 注意：pageSize=1 即可，我们只需要 total 字段

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    Console.WriteLine($"{DateTime.Now}: 正在获取待处理危急值数量...");
                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        // 复用 CriticalValueApiResponse 模型，因为它包含 total 字段
                        var apiResponse = JsonConvert.DeserializeObject<CriticalValueApiResponse>(responseBody);

                        if (apiResponse != null && apiResponse.Code == 200)
                        {
                            Console.WriteLine($"{DateTime.Now}: 成功获取待处理数量: {apiResponse.Total}");
                            return apiResponse.Total; // 返回 total 字段的值
                        }
                        else
                        {
                            Console.WriteLine($"{DateTime.Now}: 获取待处理数量失败 - API响应无效。 Code: {apiResponse?.Code}, Msg: {apiResponse?.Message}");
                            return -1; // 获取失败
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: 获取待处理数量失败 - HTTP状态码: {response.StatusCode}");
                        return -1; // 获取失败
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: 获取待处理数量时发生异常: {ex.Message}");
                return -1; // 获取失败
            }
        }

        // ProcessCriticalValueData 方法现在主要用于处理实时推送的数据（如果还需要的话）
        // 如果 `/api/criticalValue` 推送接口仍然在使用，它也应该只添加数据，
        // 而移除操作完全依赖于轮询时的同步逻辑。
        private async void ProcessCriticalValueData(List<CriticalValue> values)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ProcessCriticalValueData(values)));
                return;
            }

            bool dataAdded = false;
            int newValuesAdded = 0;

            lock (criticalValuesLock)
            {
                foreach (var value in values)
                {
                    // 仅尝试添加，HashSet 会处理重复
                    if (criticalValues.Add(value))
                    {
                        newValuesAdded++;
                        dataAdded = true; // 标记有新数据添加
                        Console.WriteLine($"{DateTime.Now}: Added item via PUSH: {value.criticalId}");
                    }
                }
            }

            // 仅在确实添加了新数据时才更新UI
            if (dataAdded)
            {
                Console.WriteLine($"{DateTime.Now}: New data added via PUSH, updating UI.");
                UpdateCriticalValueList();
                UpdateTrayIconText();
                //UpdateButtonsVisibility();
                // 更新按钮可见性（现在会考虑列表是否为空）
                ApplyCurrentButtonVisibilitySetting(); // <--- 调用更新
                // UpdateFormTitle(null, GetUnprocessedCriticalValueCount());
                // 可选：重新获取待处理数量并更新标题
                int latestProcessingCount = await FetchPendingProcessingCountAsync();
                UpdateFormTitle(null, GetUnprocessedCriticalValueCount(), latestProcessingCount >= 0 ? latestProcessingCount : (int?)null);


                StartBlinking();
                ShowAndFocusForm();
            }
        }
        // 超时获取 access_token
        private async Task<bool> TimeoutGetAccessTokenAsync(string UserId)
        {
            try
            {
                //if (!isUserLoggedIn) return false;

                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
                //var plainText = $"his_push";
                var encData = RsaHelper.Encrypt(UserId);

                var requestData = new
                {
                    userName = "his_push", // 可从配置读取
                    timestamp = timestamp,
                    encData = encData
                };

                string jsonData = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // 使用配置中的API地址和端点
                var tokenUrl = $"{ApiBaseUrl}{ConfigurationManager.AppSettings["TokenEndpoint"]}";
                HttpResponseMessage response = await httpClient.PostAsync(tokenUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                    if (tokenResponse != null && tokenResponse.ContainsKey("token"))
                    {
                        TimeoutaccessToken = tokenResponse["token"].ToString();
                        // 默认设置token有效期为1小时（可以解析JWT获取实际过期时间）
                        // tokenExpirationTime = DateTime.Now.AddHours(1);
                        TimeouttokenExpirationTime = DateTime.Now.AddMinutes(30);
                        // 更新请求头
                        // httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                        // 启动心跳定时器
                        //heartbeatTimer.Enabled = true;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取Token异常: {ex.Message}");
            }
            return false;
        }

        // 获取 access_token

        private async Task<bool> GetAccessTokenAsync()
        {
            try
            {
                if (!isUserLoggedIn) return false;

                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
                //var plainText = $"his_push";
                var encData = RsaHelper.Encrypt(currentUser.UserId);

                var requestData = new
                {
                    userName = "his_push", // 可从配置读取
                    timestamp = timestamp,
                    encData = encData
                };

                string jsonData = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // 使用配置中的API地址和端点
                var tokenUrl = $"{ApiBaseUrl}{ConfigurationManager.AppSettings["TokenEndpoint"]}";
                HttpResponseMessage response = await httpClient.PostAsync(tokenUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                    if (tokenResponse != null && tokenResponse.ContainsKey("token"))
                    {
                        accessToken = tokenResponse["token"].ToString();
                        // 默认设置token有效期为1小时（可以解析JWT获取实际过期时间）
                       // tokenExpirationTime = DateTime.Now.AddHours(1);
                        tokenExpirationTime = DateTime.Now.AddMinutes(30);
                        // 更新请求头
                        // httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                        // 启动心跳定时器
                        heartbeatTimer.Enabled = true;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取Token异常: {ex.Message}");
            }
            return false;
        }
        // 生成签名
        private string GenerateSignature(Dictionary<string, string> parameters)
        {
            // 过滤空值并排序
            var sortedParams = parameters
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(p => p.Key, p => p.Value);

            // 拼接字符串
            string stringA = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
            string stringSignTemp = $"{stringA}&key={SecretKey}";

            // 计算 MD5
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(stringSignTemp));
                string signValue = BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
                return signValue;
            }
        }
        // 发送心跳检测
        private async Task SendHeartbeatAsync()
        {
            try
            {
                // 检查 Token 是否即将过期（剩余时间小于 5 分钟）
                if (DateTime.Now >= tokenExpirationTime.AddMinutes(-5))
                {
                   // AppendTextToResult($"{DateTime.Now}: Token 即将过期，提前刷新中...\r\n");
                    //AppendTextToResult($"{DateTime.Now}: Token 即将过期，提前刷新中...\r\n");
                    bool tokenUpdated = await GetAccessTokenAsync();
                    if (!tokenUpdated)
                    {
                       // AppendTextToResult($"{DateTime.Now}: 重新获取 Token 失败，跳过本次心跳检测\r\n");
                        return;
                    }
                }
                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

                var JSONencData = new Dictionary<string, string>
                        {

                            { "userId", $"{currentUser.UserId}" },
                            { "departmentId", currentUser.DeptId },
                            { "ip", GetLocalIP() },
                            { "mac", GetMacAddress() },
                            { "userName", currentUser.UserName },
                            { "departmentName", currentUser.DepartmentName },
                            { "version", currentUser.DepartmentName }
                        };

                string jsonDataencData = JsonConvert.SerializeObject(JSONencData);

                var encData = RsaHelper.Encrypt(jsonDataencData);
                var requestData = new
                {
                    userName = "his_push", // 可从配置读取
                    timestamp = timestamp,
                    encData = encData
                };

                ////// 构造请求参数
                //var requestData = new
                //{
                //    //id = $"123_{GetLocalIP()}_{GetMacAddress()}_10003"
                //    //id = $"{currentUser.UserId}_{GetLocalIP()}_{GetMacAddress()}_{currentUser.DeptId}"
                //    //id = $"his_push_{GetLocalIP()}_{GetMacAddress()}_{currentUser.DeptId}"
                //};

                // 将参数转换为 JSON
                string jsonData = JsonConvert.SerializeObject(requestData);
          
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // 添加 token 到请求头
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // 发送 POST 请求
                // HttpResponseMessage response = await httpClient.PostAsync($"{ApiBaseUrl}/api/heartbeat/sendHeartbeat", content);
                HttpResponseMessage response = await httpClient.PostAsync($"{ApiBaseUrl}/critical/status/heartbeat", content);
                // 检查响应状态
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                    if (tokenResponse != null && tokenResponse["code"].ToString().Equals("200"))
                    {
                        heartbeatFailureCount = 0; // 重置失败计数
                    }

                   // AppendTextToResult($"{DateTime.Now}: 心跳检测成功！\r\n");
                }
                else
                {
                    heartbeatFailureCount++;
                   // AppendTextToResult($"{DateTime.Now}: 心跳检测失败，状态码：{response.StatusCode}\r\n");

                    if (heartbeatFailureCount >= 3)
                    {
                      //  AppendTextToResult($"{DateTime.Now}: 心跳检测连续失败 3 次，尝试重新获取 Token...\r\n");
                        bool tokenUpdated = await GetAccessTokenAsync();
                        if (tokenUpdated)
                        {
                            heartbeatFailureCount = 0; // 重置失败计数
                        }
                    }
                }
            }
            catch (Exception ex)
            {
               // AppendTextToResult($"{DateTime.Now}: 心跳检测异常：{ex.Message}\r\n");
            }
        }

        //private async void ShowForm()
        //{
        //    if (this.InvokeRequired)
        //    {
        //        this.Invoke(new Action(ShowForm));
        //    }
        //    else
        //    {
        //        this.Show(); // 显示窗体
        //        this.WindowState = FormWindowState.Normal; // 恢复窗体状态
        //        this.ShowInTaskbar = true; // 显示任务栏图标

        //        // 延迟 100 毫秒，确保窗体完全初始化
        //        await Task.Delay(100);

        //        SetFormPositionToBottomRight(); // 调整窗体位置
        //        this.BringToFront(); // 将窗体置于最前
        //    }
        //}

        private async void ShowForm()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ShowForm));
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                this.ShowInTaskbar = true;
                this.FormBorderStyle = FormBorderStyle.None;
                this.ControlBox = false;
                this.Show();
                this.BringToFront();
            }
        }

        //private void ProcessCriticalValueData(List<CriticalValue> values)
        //{
        //    if (this.InvokeRequired) // 检查是否需要跨线程调用
        //    {
        //        this.Invoke(new Action(() => ProcessCriticalValueData(values)));
        //        return;
        //    }
        //    int newValuesAdded = 0;
        //    bool dataAdded = false;

        //    lock (criticalValues)
        //    {
        //        foreach (var value in values)
        //        {
        //            if (criticalValues.Add(value))
        //            {

        //                //最小化和关闭按钮
        //                dataAdded = true;
        //                // 添加新数据
        //                newValuesAdded++;
        //            }
        //        }
        //    }
        //    if (dataAdded)
        //    {
        //        //UpdateButtonsVisibility(); // 添加数据后更新按钮可见性
        //    }

        //    // 如果添加了新的危急值，更新标题显示
        //    if (newValuesAdded > 0)
        //    {
        //        UpdateFormTitle(null, GetUnprocessedCriticalValueCount());
        //    }
        //    StartBlinking();
        //    ShowAndFocusForm();
        //    UpdateCriticalValueList();
        //    UpdateTrayIconText();
        //}

        private async void UpdateTrayIconText()
        {
            //int unprocessedCount = criticalValues.Count(v => !v.IsProcessed);
            //trayIcon.Text = $"危急值客户端（未处理：{unprocessedCount}）";
            // 可选：重新获取待处理数量并更新标题
            int latestProcessingCount = await FetchPendingProcessingCountAsync();
            //  UpdateFormTitle(null, GetUnprocessedCriticalValueCount(), latestProcessingCount >= 0 ? latestProcessingCount : (int?)null);
            trayIcon.Text = $"危急值客户端（未处理：{latestProcessingCount}）";

        }
        // 更新危急值列表
        //private void UpdateCriticalValueList()
        //{
        //    if (lstCriticalValues.InvokeRequired)
        //    {
        //        // 使用 BeginInvoke 避免阻塞调用线程
        //        lstCriticalValues.BeginInvoke(new Action(UpdateCriticalValueList));
        //        return;
        //    }

        //    // 清空列表前检查数据是否有效
        //    lstCriticalValues.Items.Clear();

        //    lock (criticalValues)
        //    {
        //        // 将HashSet转换为List以便可以访问索引
        //        var valuesList = criticalValues.ToList();

        //        foreach (var value in valuesList)
        //        {
        //            string deptInfo = !string.IsNullOrEmpty(value.deptName) ? $" [{value.deptName}]" : "";
        //            string examinationDate = !string.IsNullOrEmpty(value.triggerTime) ? $" [{value.triggerTime}]" : "";
        //            string itemText = $"{value.PatientName} - {value.Indicator}: {value.Value} {deptInfo} {examinationDate} ";
        //            lstCriticalValues.Items.Add(itemText);
        //        }

        //        //foreach (var value in criticalValues)
        //        //{
        //        //    string itemText = $"{value.PatientName} - {value.Indicator}: {value.Value} ({value.Status})";
        //        //    lstCriticalValues.Items.Add(itemText);
        //        //}


        //    }
        //    // 列表更新后更新按钮可见性
        //    //UpdateButtonsVisibility();
        //    // 更新按钮可见性（现在会考虑列表是否为空）
        //    ApplyCurrentButtonVisibilitySetting(); // <--- 调用更新

        //}
        // 更新危急值列表，并按 triggerTime 排序 (假设是最新的在前)
        private void UpdateCriticalValueList()
        {
            if (lstCriticalValues.InvokeRequired)
            {
                // 使用 BeginInvoke 避免阻塞调用线程
                lstCriticalValues.BeginInvoke(new Action(UpdateCriticalValueList));
                return;
            }

            // 先清除现有项
            lstCriticalValues.Items.Clear();

            List<CriticalValue> sortedValues;

            lock (criticalValuesLock) // 确保线程安全
            {
                // 1. 将 HashSet 转换为 List
                var valuesList = criticalValues.ToList();

                // 2. 对 List 进行排序
                sortedValues = valuesList.OrderByDescending(value => {
                    // 尝试将 triggerTime 解析为 DateTime 进行排序
                    if (DateTime.TryParse(value.triggerTime, out DateTime dt))
                    {
                        return dt; // 解析成功，返回 DateTime 对象
                    }
                    // 解析失败，返回一个默认值（例如，让解析失败的排在最前面或最后面）
                    // 这里让解析失败的排在最前面 (最早)
                    return DateTime.MinValue;
                }).ToList(); // OrderByDescending 会将最新的排在前面

                // 如果想让最早的排在前面，使用 OrderBy:
                // sortedValues = valuesList.OrderBy(value => { ... }).ToList();
            }

            // 3. 将排序后的结果存储到 displayedCriticalValues
            displayedCriticalValues = sortedValues; // 将排序好的List保存起来

            // 4. 遍历排序后的 List，添加项到 ListBox
            foreach (var value in sortedValues)
            {
                string deptInfo = !string.IsNullOrEmpty(value.deptName) ? $" [{value.deptName}]" : "";
                // 确保 triggerTime 显示的是原始字符串，即使排序用了 DateTime
                string triggerTimeInfo;
                if (!string.IsNullOrEmpty(value.triggerTime))
                {
                    // 尝试将 triggerTime 解析为 DateTime
                    if (DateTime.TryParse(value.triggerTime, out DateTime triggerTime))
                    {
                        // 格式化为 "yyyy-MM-dd" 格式
                        triggerTimeInfo = $" [{triggerTime.ToString("yyyy-MM-dd")}]";
                    }
                    else
                    {
                        // 如果解析失败,使用原始字符串
                        triggerTimeInfo = $" [{value.triggerTime}]";
                    }
                }
                else
                {
                    triggerTimeInfo = "";
                }

                //string triggerTimeInfo = !string.IsNullOrEmpty(value.triggerTime) ? $" [{value.triggerTime}]" : "";
                //string itemText = $"{value.PatientName} - {value.Indicator}: {value.Value}{deptInfo}{triggerTimeInfo}"; // 注意 deptInfo 和 triggerTimeInfo 之间的空格或格式
                if (value.sourceSystem.Equals("1"))
                {
                    string itemText = $"{value.PatientName} - {value.Indicator}: {value.Value}{deptInfo}{triggerTimeInfo}";
                    lstCriticalValues.Items.Add(itemText);
                }
                else {
                    string itemText = $"{value.PatientName} - {value.Indicator}: {deptInfo}{triggerTimeInfo}";
                    lstCriticalValues.Items.Add(itemText);
                }

            }

            // 列表更新后更新按钮可见性（或其他相关UI）
            ApplyCurrentButtonVisibilitySetting(); // 确保按钮状态也更新
        }

        // 显示通知
        private void ShowNotification(string message)
        {
            trayIcon.BalloonTipText = message;
            trayIcon.ShowBalloonTip(3000); // 显示 3 秒
        }
        // 获取本地 IP 地址
        //private string GetLocalIP()
        //{
        //    try
        //    {
        //        string hostName = Dns.GetHostName();
        //        IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
        //        string localIP = hostEntry.AddressList
        //            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        //            ?.ToString();
        //        localIP = "192.168.1.252";
        //        return localIP ?? "未找到 IPv4 地址";
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"获取 IP 地址失败: {ex.Message}";
        //    }
        //}
        private string GetLocalIP()
        {
            try
            {
                // 需要添加引用: using System.Net.NetworkInformation;

                // 获取所有网络接口
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface adapter in adapters)
                {
                    // 只考虑运行中的接口，排除虚拟接口等
                    if (adapter.OperationalStatus == OperationalStatus.Up &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        // 获取该接口的IP属性
                        IPInterfaceProperties ipProperties = adapter.GetIPProperties();

                        // 检查是否有默认网关
                        if (ipProperties.GatewayAddresses.Count > 0)
                        {
                            // 获取该接口的所有单播地址
                            foreach (UnicastIPAddressInformation ipInfo in ipProperties.UnicastAddresses)
                            {
                                // 只返回IPv4地址
                                if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    return ipInfo.Address.ToString();
                                }
                            }
                        }
                    }
                }

                // 未找到有默认网关的IPv4地址
                return "未找到具有默认网关的IPv4地址";
            }
            catch (Exception ex)
            {
                return $"获取IP地址失败: {ex.Message}";
            }
        }

        // 获取 MAC 地址
        private static string GetMacAddress()
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    // 获取 MAC 地址
                    string macAddress = networkInterface.GetPhysicalAddress().ToString();

                    // 格式化 MAC 地址（每两个字符用冒号分隔）
                    return string.Join(":", Enumerable.Range(0, 6)
                        .Select(i => macAddress.Substring(i * 2, 2)));
                }
            }

            return string.Empty;
        }
        // 安全更新 UI 控件
        //private void AppendTextToResult(string text)
        //{
        //    if (txtResult.InvokeRequired)
        //    {
        //        txtResult.Invoke(new Action<string>(AppendTextToResult), text);
        //    }
        //    else
        //    {
        //        txtResult.AppendText(text);
        //    }
        //}
        // 窗体关闭事件
        //protected override void OnFormClosing(FormClosingEventArgs e)
        //{
        //    // 隐藏窗口而不是关闭
        //    if (e.CloseReason == CloseReason.UserClosing)
        //    {
        //        e.Cancel = true;
        //        this.Hide();
        //    }
        //    base.OnFormClosing(e);
        //}



        // 5. 修改双击事件处理，使用新的确认并处理方法

        //private void lstCriticalValues_DoubleClick(object sender, EventArgs e)
        //{
        //    if (lstCriticalValues.SelectedIndex >= 0)
        //    {
        //        lock (criticalValuesLock)
        //        {
        //            var value = criticalValues.ElementAtOrDefault(lstCriticalValues.SelectedIndex);
        //            if (value != null)
        //            {
        //                ConfirmAndProcessCriticalValue(value);
        //            }
        //        }
        //    }
        //}
        // 5. 修改双击事件处理，使用新的确认并处理方法
        private void lstCriticalValues_DoubleClick(object sender, EventArgs e)
        {
            int index = lstCriticalValues.SelectedIndex; // 获取双击的视觉索引

            // 确保索引有效且在显示的列表中
            if (index < 0 || index >= displayedCriticalValues.Count)
                return;

            // **** 修改点：从 displayedCriticalValues 中获取对象 ****
            CriticalValue value = null;
            // 在UI事件中访问 displayedCriticalValues 通常是安全的
            value = displayedCriticalValues[index];

            // 确保成功获取到对象
            if (value != null)
            {
                Console.WriteLine($"双击了危急值 [{value.criticalId}]，准备 '确认并处理'");
                // 调用确认并处理方法
                ConfirmAndProcessCriticalValue(value);
            }
        }
        private void OpenWebPage(string patientId, string reportId)
        {
            //string url = $"http://example.com/process?patientName={patientName}&indicator={indicator}";
            string url = $"{WebAppUrl}{accessToken}&criticalId={reportId}";
            Process browserProcess = System.Diagnostics.Process.Start(url);
            //Process browserProcess = Process.Start(url);

        }
        private void TimeoutOpenWebPage(string patientId, string reportId)
        {
            //string url = $"http://example.com/process?patientName={patientName}&indicator={indicator}";
            string url = $"{WebAppUrl}{TimeoutaccessToken}&criticalId={reportId}";
            Process browserProcess = System.Diagnostics.Process.Start(url);
            //Process browserProcess = Process.Start(url);

        }


        // 修改OpenWebPage方法
        //private void OpenWebPage(string patientId, string reportId)
        //{
        //    //string url = $"{WebAppUrl}?token={accessToken}&patientID={patientId}"
        //    string url = $"{WebAppUrl}{accessToken}&criticalId={reportId}";
        //    string key = $"{patientId}_{reportId}";


        //    // 如果已有相同key的窗体，先关闭它
        //    CloseBrowser(patientId, reportId);


        //    // 创建新的浏览器窗体
        //    Form browserForm = new Form
        //    {
        //        Text = "危急值处理",
        //        //Size = new Size(1200, 800),
        //        //this.MaximizeBox = false;

        //        WindowState = FormWindowState.Maximized, // 设置为最大化
        //        MaximizeBox = false,  // 禁用最大化按钮
        //       // MinimizeBox = false,  // 禁用最小化按钮
        //        StartPosition = FormStartPosition.CenterScreen
        //    };


        //    // 创建CefSharp浏览器控件
        //    cefBrowser = new ChromiumWebBrowser(url)
        //    {
        //        Dock = DockStyle.Fill
        //    };


        //    browserForm.Controls.Add(cefBrowser);
        //    browserForm.Show();


        //    // 保存窗体引用
        //    lock (browserForms)
        //    {
        //        browserForms[key] = browserForm;
        //    }
        //}

        // 修改CloseBrowser方法
        private void CloseBrowser(string patientId, string reportId)
        {
            string key = $"{patientId}_{reportId}";

            lock (browserForms)
            {
                if (browserForms.ContainsKey(key))
                {
                    try
                    {
                        Form form = browserForms[key];
                        if (!form.IsDisposed)
                        {
                            form.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"关闭浏览器窗体异常: {ex.Message}");
                    }

                    browserForms.Remove(key);
                }
            }
        }

        /// <summary>
        /// 获取系统默认浏览器的可执行文件路径
        /// </summary>
        static string GetDefaultBrowserPath()
        {
            try
            {
                // 访问注册表，获取默认浏览器路径
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(null);
                        if (value != null)
                        {
                            string browserPath = value.ToString();

                            // 解析路径，去掉可能的参数
                            if (browserPath.StartsWith("\""))
                            {
                                browserPath = browserPath.Split('\"')[1]; // 处理路径带引号的情况
                            }
                            else
                            {
                                browserPath = browserPath.Split(' ')[0]; // 处理无引号但带参数的情况
                            }
                            return browserPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取默认浏览器失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 关闭指定的浏览器进程
        /// </summary>
        static void CloseBrowserProcess(string browserExe)
        {
            try
            {
                Console.WriteLine($"尝试关闭 {browserExe} 进程...");
                Process.Start("taskkill", $"/IM {browserExe} /F");
                Console.WriteLine($"{browserExe} 进程已终止。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭浏览器失败: {ex.Message}");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // 关闭所有打开的浏览器
            if (openBrowserProcesses != null)
            {
                lock (openBrowserProcesses)
                {
                    foreach (var process in openBrowserProcesses.Values)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.CloseMainWindow();
                            }
                        }
                        catch { }
                    }
                    openBrowserProcesses.Clear();
                }
            }

            // 关闭所有浏览器窗体
            if (browserForms != null)
            {
                lock (browserForms)
                {
                    foreach (var form in browserForms.Values)
                    {
                        try
                        {
                            if (!form.IsDisposed)
                            {
                                form.Close();
                            }
                        }
                        catch { }
                    }
                    browserForms.Clear();
                }
            }
        }

        //// UpdateInfo 类定义 (如果还没定义，请添加)
        //public class UpdateInfo
        //{
        //    public string LatestVersion { get; set; }
        //    public string DownloadUrl { get; set; } // 本地测试时，这将是本地文件路径
        //    public string UpdaterDownloadUrl { get; set; } // 本地测试时，这也将是本地文件路径
        //    public string UpdaterVersion { get; set; }
        //    public string ReleaseNotes { get; set; }
        //    public bool IsMandatory { get; set; }
        //    public string Checksum { get; set; }
        //}
        public class ApiResponse<T>
        {
            public string Msg { get; set; }
            public int Code { get; set; }
            public T Data { get; set; }
        }

        public class UpdateInfo
        {
            public string CreateBy { get; set; }
            public string CreateTime { get; set; }
            public string UpdateBy { get; set; }
            public string UpdateTime { get; set; }
            public string Remark { get; set; }
            public string VersionId { get; set; }
            public string ReleaseNotes { get; set; }
            public string DownloadUrl { get; set; }
            public string Checksum { get; set; }
            public int IsMandatory { get; set; }
            public string MinSupportedVersion { get; set; }
            public string PublishTime { get; set; }
            public string Platform { get; set; }
        }



        /// <summary>
        /// 验证是否本人操作
        /// </summary>
        /// <param name="strJsonIn">{"LoginUser":"字符串|登录用户","Scene":"数字|场景序号"}</param>
        /// <param name="strJsonOut">{"Result":"数字|检查结果:1-通过,0-不通过"}</param>
        /// <returns></returns>
        public bool APP_CheckCertificate(string strJsonIn, out string strJsonOut)
        {
            string strInfo;
            JObject objJson;
            string strUser;
            Boolean blnResult;
            string strCert = "";
           // string EncCert = "";
            string SignCert = "";

            ClsPublic.WriteLog("校验是否本人操作", 3);
            //strJsonOut = "{\"Result\":\"0\"}";
            strJsonOut = "";
            try
            {
                objJson = JObject.Parse(strJsonIn);
                strUser = objJson["LoginUser"].ToString();

                #region 校验操作员信息
                ClsPublic.WriteLog("证书类型为APP证书，校验是否本人操作", 3);
                //开启时效签
                if (!ClsCertFunc.StartAutoSign("", out string strsignDataId, out string strQrCode))
                {
                    MessageBox.Show("开启时效签失败，请重试！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                ClsPublic.WriteLog("开启自动签名任务成功，展示二维码让操作员扫码验证", 3);
                ShowImg img = new ShowImg();
                //strInfo = img.GetQrCodeInfo(strQrCode, strsignDataId, 2, CertSN);
                //获取userid
                strInfo = img.GetQrCodeInfo(strQrCode, strsignDataId, 2, "");
                //根据userid获取用户信息               
                if (!ClsCertFunc.QueryUserInfo(strInfo, out string CertCN, out string CertDN, out string CertSN, out string EncCert))
                {
                    ClsPublic.WriteLog("获取用户信息失败，请重试！", 1);
                    MessageBox.Show("获取用户信息失败，请重试！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }


                 strJsonOut = (CertCN?.StartsWith("U",StringComparison.OrdinalIgnoreCase) ?? false) ? CertCN : "U" + CertCN;
                //if (strInfo == "1")
                //    {
                //        ClsPublic.gdtExemptTime = DateTime.Now;
                //        ClsPublic.WriteLog("免验证签名开启成功", 3);
                //        strJsonOut = "{\"Result\":\"1\"}";
                //        ClsPublic.gblnAuto = true;
                //    }
                //    else
                //    {
                //        ClsPublic.WriteLog("免验证签名开启失败", 1);
                //        MessageBox.Show("免验证签名开启失败", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                //    }

                #endregion
            }
            catch (Exception ex)
            {
                //if (ClsPublic.gOraConn.State == ConnectionState.Open)
                //{
                //    //dr.Close();
                //    cmd.Dispose();
                //    ClsPublic.gOraConn.Close();
                //}
                ClsPublic.WriteLog("医护人员身份验证失败，" + ex.Message, 1);
                MessageBox.Show("医护人员身份验证失败,不能继续完成签名！" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return true;
        }

    }

    // Token 响应模型
    public class TokenResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public TokenData Data { get; set; }
    }

    public class TokenResponseNew
    {
        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }

    public class TokenData
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }

    // 心跳检测响应模型
    public class HeartbeatResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    // 危急值数据模型
    public class CriticalValue
    {
        public string PatientName { get; set; }//姓名
        public string Indicator { get; set; }//检验项目名称
        public string Value { get; set; }//结果
        public string Status { get; set; }//状态
        public string patientId { get; set; }//病人id
        public string itemCode { get; set; }//检验项目编码

        public string criticalId { get; set; }//危急值ID

        public string deptName { get; set; } //科室名称

        public bool IsProcessed { get; set; } // 新增属性

        public string triggerTime { get; set; } // 危急值触发时间


        public string sourceSystem { get; set; } // 危急值触发时间
        //public override bool Equals(object obj)
        //{
        //    if (obj is CriticalValue other)
        //    {
        //        return patientId == other.patientId &&
        //               criticalId == other.criticalId &&
        //               itemCode == other.itemCode;
        //    }
        //    return false;
        //}

        //public override int GetHashCode()
        //{
        //    return (patientId + criticalId + itemCode).GetHashCode();
        //}
        public override bool Equals(object obj)
        {
            if (obj is CriticalValue other && !string.IsNullOrEmpty(criticalId))
            {
                // Primarily rely on criticalId for uniqueness
                return criticalId == other.criticalId;
            }
            // Fallback or handle cases where criticalId might be missing (less likely for API data)
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            // Primarily rely on criticalId for hash code
            if (!string.IsNullOrEmpty(criticalId))
            {
                return criticalId.GetHashCode();
            }
            // Fallback
            // Consider combining other potentially unique fields if criticalId can be null
            // return (patientId + reportId + itemCode ?? "").GetHashCode(); // Example fallback
            return base.GetHashCode(); // Basic fallback
        }

    }
    public class ProcessedData
    {
        public string patientId { get; set; }
        public string CriticalId { get; set; }
        public string itemCode { get; set; }
        // 保留原始属性用于兼容
        public string PatientName { get; set; }
        public string Indicator { get; set; }
    }
    //单点登录用户信息
    public class UserLoginInfo
    {
        public string UserId { get; set; }
        public string DeptId { get; set; }
        public string UserName { get; set; }
        public string DepartmentName { get; set; }

        public string Nature { get; set; }
    }

    // Model for the overall API response
    public class CriticalValueApiResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("rows")]
        public List<ApiCriticalValueRow> Rows { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }
    }
    // Model for each item in the "rows" array
    public class ApiCriticalValueRow
    {
        [JsonProperty("criticalId")]
        public string CriticalId { get; set; }

        [JsonProperty("patientId")]
        public string PatientId { get; set; }

        [JsonProperty("patientName")]
        public string PatientName { get; set; }

        // Map itemName from API to Indicator in our model logic
        [JsonProperty("itemName")]
        public string ItemName { get; set; }

        // Map criticalValue from API to Value in our model logic
        [JsonProperty("criticalValue")]
        public string CriticalValueString { get; set; } // Name distinct from CriticalValue class property

        [JsonProperty("triggerTime")]
        public string TriggerTime { get; set; }

        
        [JsonProperty("sourceSystem")]
        public string sourceSystem { get; set; }
        [JsonProperty("deptName")]
        public string DeptName { get; set; }

        [JsonProperty("confirmStatus")]
        public string ConfirmStatus { get; set; }

        // Add other fields if needed for future logic
        [JsonProperty("reportId")]
        public string ReportId { get; set; } // May potentially map to itemCode if needed

        [JsonProperty("creatorId")]
        public string CreatorId { get; set; }

        [JsonProperty("timeoutCount")]
        public int TimeoutCount { get; set; } // 新增：超时计数
        // ... add other fields from the API response if required ...
    }
    // 用于解析配置API响应的模型
    public class ConfigResponse
    {
        [JsonProperty("msg")]
        public string Message { get; set; } // 服务器返回 "1" 或 "0"

        [JsonProperty("code")]
        public int Code { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace CatchCapture.Models
{
    public static class LocalizationManager
    {
        private static string _currentLanguage = "ko";
        private static Dictionary<string, Dictionary<string, string>> _translations = new();
        public static event EventHandler? LanguageChanged;

        static LocalizationManager()
        {
            InitializeTranslations();
        }

        public static string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                SetLanguage(value);
            }
        }

        public static void SetLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return;
            if (!_translations.ContainsKey(language)) return;
            if (_currentLanguage == language) return;

            _currentLanguage = language;
            try { LanguageChanged?.Invoke(null, EventArgs.Empty); } catch { }
        }

        public static string Get(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) && 
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }
            return key; // 번역이 없으면 키 자체를 반환
        }

        private static void InitializeTranslations()
        {
            // 한국어
            _translations["ko"] = new Dictionary<string, string>
            {
                // 메인 메뉴
                ["AreaCapture"] = "영역 캡처",
                ["DelayCapture"] = "지연 캡처",
                ["RealTimeCapture"] = "순간 캡처",
                ["MultiCapture"] = "멀티 캡처",
                ["FullScreen"] = "전체 화면",
                ["DesignatedCapture"] = "지정 캡처",
                ["WindowCapture"] = "창 캡처",
                ["ElementCapture"] = "단위 캡처",
                ["ScrollCapture"] = "스크롤 캡처",
                
                // 설정 공통
                ["Settings"] = "설정",
                ["SystemSettings"] = "시스템 설정",
                ["CaptureSettings"] = "캡처 설정",
                ["HotkeySettings"] = "단축키 설정",
                ["Language"] = "언어",
                ["StartWithWindows"] = "Windows 시작 시 자동 실행",
                ["StartupMode"] = "시작 모드",
                ["Normal"] = "일반모드",
                ["Simple"] = "간편모드",
                ["Tray"] = "트레이모드",
                ["General"] = "일반모드",
                ["SaveSettings"] = "저장 설정",
                ["SavePath"] = "저장 경로",
                ["Change"] = "변경",
                ["FileFormat"] = "파일 포맷",
                ["Quality"] = "품질",
                ["Options"] = "옵션",
                ["AutoSaveCapture"] = "캡처 시 자동으로 파일 저장",
                ["ShowPreviewAfterCapture"] = "캡처 후 미리보기/편집 창 표시",
                ["UsePrintScreen"] = "Print Screen 키를 사용",
                ["StartInTray"] = "트레이 모드로 시작",
                ["StartInNormal"] = "일반 모드로 시작",
                ["StartInSimple"] = "간편 모드로 시작",
                ["LanguageSettings"] = "언어 설정",
                ["LanguageLabel"] = "언어 (Language)",
                ["OpenSettings"] = "설정 열기",
                ["InstantEdit"] = "즉시편집",
                
                // 버튼
                ["Save"] = "저장",
                ["Cancel"] = "취소",
                ["OK"] = "확인",
                ["Apply"] = "적용",
                ["Close"] = "닫기",
                ["CopySelected"] = "복사",
                ["CopyAll"] = "전체복사",
                ["SaveAll"] = "전체저장",
                ["Delete"] = "삭제",
                ["DeleteAll"] = "전체삭제",
                
                // 트레이 메뉴
                ["TrayNormalMode"] = "일반 모드",
                ["TraySimpleMode"] = "간편 모드",
                ["TrayTrayMode"] = "트레이 모드",
                ["Exit"] = "종료",
                ["AppName"] = "CatchCapture",
                
                // 메시지
                ["SettingsSaved"] = "설정이 저장되었습니다.",
                ["SettingsApplied"] = "설정이 적용되었습니다.",
                ["SettingsSaveFailed"] = "설정 저장 실패",
                ["RestartRequired"] = "언어 변경을 적용하려면 프로그램을 다시 시작해야 합니다.",
                
                // 추가 키
                ["Delay3Sec"] = "3초 후 캡처",
                ["Delay5Sec"] = "5초 후 캡처",
                ["Delay10Sec"] = "10초 후 캡처",
                ["AddIcon"] = "아이콘 추가",
                ["DeleteIcon"] = "아이콘 삭제",
                
                // 미리보기/편집
                ["ImageEditTitle"] = "이미지 편집",
                ["CopyToClipboard"] = "클립보드에 복사",
                ["UndoLbl"] = "실행취소",
                ["RedoLbl"] = "다시실행",
                ["ResetLbl"] = "초기화",
                ["CropLbl"] = "자르기",
                ["RotateLbl"] = "회전",
                ["FlipHLbl"] = "좌우반전",
                ["FlipVLbl"] = "상하반전",
                ["ShapeLbl"] = "도형",
                ["ShapeOptions"] = "도형 옵션",
                ["ToolOptions"] = "도구 옵션",
                ["OCR"] = "OCR",
                ["Extract"] = "추출",
                ["ZoomOut"] = "축소",
                ["ZoomIn"] = "확대",
                ["ZoomReset"] = "원본",
                ["ZoomResetTooltip"] = "100%",
                ["RecentCaptures"] = "최근 캡처 목록",
                ["SizeLabel"] = "크기",
                ["ImageSizePrefix"] = "이미지 사이즈 : ",
                ["ImageSaved"] = "이미지가 저장되었습니다.",
                ["ImageSaveTitle"] = "이미지 저장",
                ["NoExtractedText"] = "추출된 텍스트가 없습니다.",
                ["OcrResultTitle"] = "텍스트 추출 결과",
                ["ExtractedText"] = "추출된 텍스트",
                ["Info"] = "알림",
                ["Error"] = "오류",
                ["Confirm"] = "확인",
                ["ConfirmReset"] = "모든 편집 내용을 취소하고 원본 이미지로 되돌리시겠습니까?",
                
                // SnippingWindow
                ["SelectAreaGuide"] = "영역을 선택하세요 (ESC 키를 눌러 취소)",
                ["SelectionTooSmall"] = "선택된 영역이 너무 작습니다. 다시 선택해주세요.",
                ["Color"] = "색상",
                ["TextOptions"] = "텍스트 옵션",
                ["Font"] = "폰트",
                ["Thickness"] = "두께",
                ["Fill"] = "채우기",
                ["FillOpacity"] = "채우기 투명도",
                ["Intensity"] = "강도",
                ["NoImageForOcr"] = "OCR을 수행할 이미지가 없습니다.",
                ["OcrError"] = "텍스트 추출 중 오류",
                ["Copied"] = "복사되었습니다",
                ["NoImageToSave"] = "저장할 이미지가 없습니다.",
                ["SaveError"] = "파일 저장 중 오류",
                
                // 이미지 검색 (PreviewWindow)
                ["ImageSearch"] = "IMG검색",
                ["ImageSearchTooltip"] = "이미지로 검색",
                
                // 편집 도구
                ["Pen"] = "펜",
                ["Highlighter"] = "형광펜",
                ["Text"] = "텍스트",
                ["TextAdd"] = "텍스트 추가",
                ["Mosaic"] = "모자이크",
                ["Eraser"] = "지우개",
                ["Numbering"] = "넘버링",
                
                // 공통 UI
                ["OK"] = "확인",
                ["Cancel"] = "취소",
                ["Error"] = "오류",
                ["Info"] = "알림",
                ["Crop"] = "자르기",
                ["Rotate"] = "회전",
                ["FlipH"] = "좌우반전",
                ["FlipV"] = "상하반전",
                ["PixelSizeLabel"] = "픽셀 크기:",
                ["AddColor"] = "색상 추가",
                ["ShapeType"] = "도형 유형",
                ["LineStyle"] = "선 스타일",
                ["Outline"] = "윤곽선",
                ["Style"] = "스타일",
                ["Bold"] = "굵게",
                ["Italic"] = "기울임",
                ["Underline"] = "밑줄",
                ["Shadow"] = "그림자",
                
                // 스티커/가이드
                ["CopiedToClipboard"] = "클립보드에 복사되었습니다",
                ["Captured"] = "캡처되었습니다.",
                ["ClickWindowThenEnter"] = "캡처할 창을 클릭한 후 Enter 키를 누르세요",
                ["PressEnterToStartScroll"] = "Enter 키를 눌러 스크롤 캡처를 시작하세요",
                ["WindowNotFound"] = "캡처할 창을 찾을 수 없습니다",
                ["StartingScrollCapture"] = "스크롤 캡처를 시작합니다...",
                ["NoScrollInWindow"] = "스크롤이 없는 창입니다",
                ["NoScrollableContent"] = "스크롤 가능한 내용이 없습니다",
                ["ScrollCompletedDuplicate"] = "스크롤 완료됨 (중복 감지)",
                ["ReachedMaxCaptures"] = "최대 캡처 수에 도달했습니다",
                ["ReachedMaxScrolls"] = "최대 스크롤 횟수에 도달했습니다",
                ["RealTimeF1Guide"] = "원하는 화면을 띄우고 [F1] 키를 누르세요\n(취소: ESC)",
                ["MultiCaptureHud"] = "ENTER : 합성\nF1 : 개별저장\nESC : 종료",
                ["ScrollClickToStart"] = "마우스 클릭: 스크롤 캡처 시작",
                ["EscToCancel"] = "중단",
                
                // Bottom actions & confirmations
                ["AllCopiedToClipboard"] = "전체가 클립보드에 복사되었습니다",
                ["AllImagesSaved"] = "모든 이미지가 저장되었습니다",
                ["UnsavedImageDeleteConfirm"] = "저장하지 않은 이미지를 삭제하시겠습니까?",
                ["UnsavedImagesDeleteConfirm"] = "저장하지 않은 이미지가 있습니다. 모두 삭제하시겠습니까?",
                ["ClipboardCopyFailed"] = "클립보드 복사에 실패했습니다",
                ["CopyError"] = "복사 오류",
                
                // Tooltips & common
                ["Minimize"] = "최소화",
                ["Close"] = "닫기",
                ["NoDelay"] = "지연없음",
                
                // Print preview
                ["PrintPreviewTitle"] = "인쇄 미리보기",
                ["OrientationLabel"] = "방향:",
                ["Portrait"] = "세로",
                ["Landscape"] = "가로",
                ["PrintOptionsLabel"] = "인쇄 옵션:",
                ["FitToPage"] = "페이지에 맞춤",
                ["ActualSize"] = "실제 크기",
                ["FillPage"] = "페이지 채우기",
                ["Print"] = "인쇄",
                ["PreviewGenerationError"] = "미리보기 생성 중 오류",
                ["PrintingStarted"] = "인쇄를 시작했습니다.",
                ["PrintingError"] = "인쇄 중 오류",
                ["NoImageToPrint"] = "인쇄할 이미지가 없습니다.",
                ["PrintPreviewError"] = "인쇄 미리보기 중 오류가 발생했습니다",
                ["RecaptureError"] = "재캡처 중 오류가 발생했습니다",
                
                // Settings window extras
                ["Version"] = "버전",
                ["VisitHomepage"] = "홈페이지 방문",
                ["RestoreDefaults"] = "기본설정 복원",
                ["PrivacyPolicy"] = "개인정보 처리방침",
                ["StartupModeNotice"] = "※ 시작 모드는 프로그램이 실행될 때 처음 표시되는 화면을 설정합니다.",
                
                // PreviewWindow extras
                ["Recapture"] = "재캡처",
                
                // MainWindow: Open dialog / errors / topmost
                ["OpenImageTitle"] = "이미지 열기",
                ["ImageFilesFilter"] = "이미지 파일",
                ["AllFiles"] = "모든 파일",
                ["FileOpenErrorTitle"] = "오류",
                ["FileOpenErrorPrefix"] = "파일을 열 수 없습니다",
                ["TopmostOnMsg"] = "상단 고정: 켜짐",
                ["TopmostOffMsg"] = "상단 고정: 꺼짐",
            };

            // 중국어 (简体中文)
            _translations["zh"] = new Dictionary<string, string>
            {
                // 메인 메뉴 (中文保留“截图”后缀)
                ["AreaCapture"] = "区域截图",
                ["DelayCapture"] = "延时截图",
                ["RealTimeCapture"] = "实时截图",
                ["MultiCapture"] = "多重截图",
                ["FullScreen"] = "全屏截图",
                ["DesignatedCapture"] = "指定截图",
                ["WindowCapture"] = "窗口截图",
                ["ElementCapture"] = "元素截图",
                ["ScrollCapture"] = "滚动截图",
                
                // 設定統合
                ["Settings"] = "设置",
                ["SystemSettings"] = "系统设置",
                ["CaptureSettings"] = "截图设置",
                ["HotkeySettings"] = "快捷键设置",
                ["Language"] = "语言",
                ["StartWithWindows"] = "开机自动启动",
                ["StartupMode"] = "启动模式",
                ["Normal"] = "普通",
                ["Simple"] = "简单",
                ["Tray"] = "托盘",
                ["General"] = "常规",
                ["SaveSettings"] = "保存设置",
                ["SavePath"] = "保存路径",
                ["Change"] = "更改",
                ["FileFormat"] = "文件格式",
                ["Quality"] = "质量",
                ["Options"] = "选项",
                ["AutoSaveCapture"] = "截图后自动保存",
                ["ShowPreviewAfterCapture"] = "截图后显示预览/编辑窗口",
                ["UsePrintScreen"] = "使用 Print Screen 键",
                ["StartInTray"] = "以托盘模式启动",
                ["StartInNormal"] = "以普通模式启动",
                ["StartInSimple"] = "以简易模式启动",
                ["LanguageSettings"] = "语言设置",
                ["LanguageLabel"] = "语言",
                ["OpenSettings"] = "打开设置",
                ["InstantEdit"] = "即时编辑",
                
                // 버튼
                ["Save"] = "保存",
                ["Cancel"] = "取消",
                ["OK"] = "确定",
                ["Apply"] = "应用",
                ["Close"] = "关闭",
                ["CopySelected"] = "复制",
                ["CopyAll"] = "全部复制",
                ["SaveAll"] = "全部保存",
                ["Delete"] = "删除",
                ["DeleteAll"] = "全部删除",
                
                // 트레이 메뉴
                ["TrayNormalMode"] = "普通模式",
                ["TraySimpleMode"] = "简易模式",
                ["TrayTrayMode"] = "托盘模式",
                ["Exit"] = "退出",
                ["AppName"] = "CatchCapture",
                
                // 메시지
                ["SettingsSaved"] = "设置已保存",
                ["SettingsApplied"] = "设置已应用",
                ["SettingsSaveFailed"] = "保存设置失败",
                ["RestartRequired"] = "需要重新启动程序才能应用语言更改",
                
                // 기타
                ["Delay3Sec"] = "3秒后截图",
                ["Delay5Sec"] = "5秒后截图",
                ["Delay10Sec"] = "10秒后截图",
                ["AddIcon"] = "添加图标",
                ["DeleteIcon"] = "删除图标",
                
                // 미리보기/편집
                ["ImageEditTitle"] = "图片编辑",
                ["CopyToClipboard"] = "复制到剪贴板",
                ["UndoLbl"] = "撤销",
                ["RedoLbl"] = "重做",
                ["ResetLbl"] = "重置",
                ["CropLbl"] = "裁剪",
                ["RotateLbl"] = "旋转",
                ["FlipHLbl"] = "左右翻转",
                ["FlipVLbl"] = "上下翻转",
                ["ShapeLbl"] = "形状",
                ["ShapeOptions"] = "形状选项",
                ["ToolOptions"] = "工具选项",
                ["OCR"] = "OCR",
                ["Extract"] = "提取",
                ["ZoomOut"] = "缩小",
                ["ZoomIn"] = "放大",
                ["ZoomReset"] = "原始",
                ["ZoomResetTooltip"] = "100%",
                ["RecentCaptures"] = "最近截图列表",
                ["SizeLabel"] = "大小",
                ["ImageSizePrefix"] = "图片大小：",
                ["ImageSaved"] = "图片已保存。",
                ["ImageSaveTitle"] = "保存图片",
                ["NoExtractedText"] = "没有提取到文本。",
                ["OcrResultTitle"] = "文字提取结果",
                ["ExtractedText"] = "提取的文本",
                ["Info"] = "信息",
                ["Error"] = "错误",
                ["Confirm"] = "确认",
                ["ConfirmReset"] = "是否撤销所有编辑并恢复到原始图片？",
                
                // SnippingWindow
                ["SelectAreaGuide"] = "请选择区域（按 ESC 取消）",
                ["SelectionTooSmall"] = "选择的区域太小，请重新选择。",
                ["Color"] = "颜色",
                ["TextOptions"] = "文字选项",
                ["Font"] = "字体",
                ["Thickness"] = "粗细",
                ["Fill"] = "填充",
                ["FillOpacity"] = "填充不透明度",
                ["Intensity"] = "强度",
                ["NoImageForOcr"] = "没有可用于 OCR 的图像。",
                ["OcrError"] = "文字提取时发生错误",
                ["Copied"] = "已复制",
                ["NoImageToSave"] = "没有可保存的图像。",
                ["SaveError"] = "文件保存时出错",
                
                // 이미지 검색 (PreviewWindow)
                ["ImageSearch"] = "以图搜图",
                ["ImageSearchTooltip"] = "以图搜索",
                
                // 편집 도구
                ["Pen"] = "画笔",
                ["Highlighter"] = "荧光笔",
                ["Text"] = "文字",
                ["TextAdd"] = "添加文字",
                ["Mosaic"] = "马赛克",
                ["Eraser"] = "橡皮",
                ["Numbering"] = "编号",
                
                // 共通 UI
                ["OK"] = "确定",
                ["Cancel"] = "取消",
                ["Error"] = "错误",
                ["Info"] = "提示",
                ["Crop"] = "裁剪",
                ["Rotate"] = "旋转",
                ["FlipH"] = "水平翻转",
                ["FlipV"] = "垂直翻转",
                ["PixelSizeLabel"] = "像素大小：",
                ["AddColor"] = "添加颜色",
                ["ShapeType"] = "图形类型",
                ["LineStyle"] = "线型",
                ["Outline"] = "描边",
                ["Style"] = "样式",
                ["Bold"] = "加粗",
                ["Italic"] = "斜体",
                ["Underline"] = "下划线",
                ["Shadow"] = "阴影",
                
                // 贴士/指引
                ["CopiedToClipboard"] = "已复制到剪贴板",
                ["Captured"] = "已截图。",
                ["ClickWindowThenEnter"] = "点击要捕获的窗口后按 Enter",
                ["PressEnterToStartScroll"] = "按 Enter 开始滚动截图",
                ["WindowNotFound"] = "找不到可捕获的窗口",
                ["StartingScrollCapture"] = "开始滚动截图...",
                ["NoScrollInWindow"] = "该窗口没有可滚动内容",
                ["NoScrollableContent"] = "没有可滚动内容",
                ["ScrollCompletedDuplicate"] = "滚动完成（检测到重复）",
                ["ReachedMaxCaptures"] = "已达到最大截图数",
                ["ReachedMaxScrolls"] = "已达到最大滚动次数",
                ["RealTimeF1Guide"] = "切换到想要的画面后按 [F1]\n（取消：ESC）",
                ["MultiCaptureHud"] = "ENTER：合成\nF1：分别保存\nESC：退出",
                ["ScrollClickToStart"] = "单击开始滚动截图",
                ["EscToCancel"] = "取消",
                
                // Bottom actions & confirmations
                ["AllCopiedToClipboard"] = "已全部复制到剪贴板",
                ["AllImagesSaved"] = "所有图片已保存",
                ["UnsavedImageDeleteConfirm"] = "要删除未保存的图片吗？",
                ["UnsavedImagesDeleteConfirm"] = "包含未保存的图片。要全部删除吗？",
                ["ClipboardCopyFailed"] = "复制到剪贴板失败",
                ["CopyError"] = "复制错误",
                
                // Tooltips & common
                ["Minimize"] = "最小化",
                ["Close"] = "关闭",
                ["NoDelay"] = "无延迟",
                
                // Print preview
                ["PrintPreviewTitle"] = "打印预览",
                ["OrientationLabel"] = "方向:",
                ["Portrait"] = "纵向",
                ["Landscape"] = "横向",
                ["PrintOptionsLabel"] = "打印选项:",
                ["FitToPage"] = "适合页面",
                ["ActualSize"] = "实际大小",
                ["FillPage"] = "填充页面",
                ["Print"] = "打印",
                ["PreviewGenerationError"] = "预览生成错误",
                ["PrintingStarted"] = "已开始打印。",
                ["PrintingError"] = "打印中出错",
                ["NoImageToPrint"] = "没有可打印的图片。",
                ["PrintPreviewError"] = "打印预览中出错",
                ["RecaptureError"] = "重新截图中出错",
                
                // Settings window extras
                ["Version"] = "版本",
                ["VisitHomepage"] = "访问主页",
                ["RestoreDefaults"] = "恢复默认设置",
                ["PrivacyPolicy"] = "隐私政策",
                ["StartupModeNotice"] = "※ 启动模式用于设置程序启动时首先显示的界面。",
                
                // PreviewWindow extras
                ["Recapture"] = "重新截图",
                
                // MainWindow: Open dialog / errors / topmost
                ["OpenImageTitle"] = "打开图片",
                ["ImageFilesFilter"] = "图像文件",
                ["AllFiles"] = "所有文件",
                ["FileOpenErrorTitle"] = "错误",
                ["FileOpenErrorPrefix"] = "无法打开文件",
                ["TopmostOnMsg"] = "置顶：开启",
                ["TopmostOffMsg"] = "置顶：关闭",
            };

            // 일본어
            _translations["ja"] = new Dictionary<string, string>
            {
                // 메인 메뉴
                ["AreaCapture"] = "範囲",
                ["DelayCapture"] = "遅延",
                ["RealTimeCapture"] = "瞬間",
                ["MultiCapture"] = "マルチ",
                ["FullScreen"] = "全画面",
                ["DesignatedCapture"] = "指定",
                ["WindowCapture"] = "ウィンドウ",
                ["ElementCapture"] = "要素",
                ["ScrollCapture"] = "スクロール",
                
                // 設定共通
                ["Settings"] = "設定",
                ["SystemSettings"] = "システム設定",
                ["CaptureSettings"] = "キャプチャ設定",
                ["HotkeySettings"] = "ショートカット設定",
                ["Language"] = "言語",
                ["StartWithWindows"] = "Windows起動時に自動実行",
                ["StartupMode"] = "起動モード",
                ["Normal"] = "通常",
                ["Simple"] = "シンプル",
                ["Tray"] = "トレイ",
                ["General"] = "一般",
                ["SaveSettings"] = "保存設定",
                ["SavePath"] = "保存先",
                ["Change"] = "変更",
                ["FileFormat"] = "ファイル形式",
                ["Quality"] = "品質",
                ["Options"] = "オプション",
                ["AutoSaveCapture"] = "キャプチャ後に自動保存",
                ["ShowPreviewAfterCapture"] = "キャプチャ後にプレビュー/編集ウィンドウを表示",
                ["UsePrintScreen"] = "Print Screen キーを使用",
                ["StartInTray"] = "トレイモードで開始",
                ["StartInNormal"] = "通常モードで開始",
                ["StartInSimple"] = "シンプルモードで開始",
                ["LanguageSettings"] = "言語設定",
                ["LanguageLabel"] = "言語 (Language)",
                ["OpenSettings"] = "設定を開く",
                ["InstantEdit"] = "即時編集",
                
                // ボタン
                ["Save"] = "保存",
                ["Cancel"] = "キャンセル",
                ["OK"] = "OK",
                ["Apply"] = "適用",
                ["Close"] = "閉じる",
                ["CopySelected"] = "コピー",
                ["CopyAll"] = "すべてコピー",
                ["SaveAll"] = "すべて保存",
                ["Delete"] = "削除",
                ["DeleteAll"] = "すべて削除",
                
                // トレイメニュー
                ["TrayNormalMode"] = "通常モード",
                ["TraySimpleMode"] = "シンプルモード",
                ["TrayTrayMode"] = "トレイモード",
                ["Exit"] = "終了",
                ["AppName"] = "CatchCapture",
                
                // メッセージ
                ["SettingsSaved"] = "設定が保存されました",
                ["SettingsApplied"] = "設定が適用されました",
                ["SettingsSaveFailed"] = "設定の保存に失敗しました",
                ["RestartRequired"] = "言語変更を適用するにはプログラムを再起動する必要があります",
                
                // その他
                ["Delay3Sec"] = "3秒後にキャプチャ",
                ["Delay5Sec"] = "5秒後にキャプチャ",
                ["Delay10Sec"] = "10秒後にキャプチャ",
                ["AddIcon"] = "アイコンを追加",
                ["DeleteIcon"] = "アイコン削除",
                
                // ミリビュー/編集
                ["ImageEditTitle"] = "画像編集",
                ["CopyToClipboard"] = "クリップボードにコピー",
                ["UndoLbl"] = "元に戻す",
                ["RedoLbl"] = "やり直し",
                ["ResetLbl"] = "リセット",
                ["CropLbl"] = "切り取り",
                ["RotateLbl"] = "回転",
                ["FlipHLbl"] = "左右反転",
                ["FlipVLbl"] = "上下反転",
                ["ShapeLbl"] = "図形",
                ["ShapeOptions"] = "図形オプション",
                ["ToolOptions"] = "ツールオプション",
                ["OCR"] = "OCR",
                ["Extract"] = "抽出",
                ["ZoomOut"] = "縮小",
                ["ZoomIn"] = "拡大",
                ["ZoomReset"] = "原本",
                ["ZoomResetTooltip"] = "100%",
                ["RecentCaptures"] = "最近のキャプチャ一覧",
                ["SizeLabel"] = "サイズ",
                ["ImageSizePrefix"] = "画像サイズ：",
                ["ImageSaved"] = "画像が保存されました。",
                ["ImageSaveTitle"] = "画像の保存",
                ["NoExtractedText"] = "抽出されたテキストがありません。",
                ["OcrResultTitle"] = "テキスト抽出結果",
                ["ExtractedText"] = "抽出されたテキスト",
                ["Info"] = "情報",
                ["Error"] = "エラー",
                ["Confirm"] = "確認",
                ["ConfirmReset"] = "すべての編集を取り消して元の画像に戻しますか？",
                
                // SnippingWindow
                ["SelectAreaGuide"] = "領域を選択してください（ESCでキャンセル）",
                ["SelectionTooSmall"] = "選択範囲が小さすぎます。もう一度選択してください。",
                ["Color"] = "色",
                ["TextOptions"] = "テキストオプション",
                ["Font"] = "フォント",
                ["Thickness"] = "太さ",
                ["Fill"] = "塗りつぶし",
                ["FillOpacity"] = "塗りつぶしの不透明度",
                ["Intensity"] = "強度",
                ["NoImageForOcr"] = "OCRを実行する画像がありません。",
                ["OcrError"] = "テキスト抽出中にエラー",
                ["Copied"] = "コピーしました",
                ["NoImageToSave"] = "保存する画像がありません。",
                ["SaveError"] = "ファイル保存中にエラー",
                
                // 이미지 검색 (PreviewWindow)
                ["ImageSearch"] = "画像検索",
                ["ImageSearchTooltip"] = "画像で検索",
                
                // 편집 도구
                ["Pen"] = "ペン",
                ["Highlighter"] = "蛍光ペン",
                ["Text"] = "テキスト",
                ["TextAdd"] = "テキスト追加",
                ["Mosaic"] = "モザイク",
                ["Eraser"] = "消しゴム",
                ["Numbering"] = "番号付け",
                
                // 共通 UI
                ["OK"] = "OK",
                ["Cancel"] = "キャンセル",
                ["Error"] = "エラー",
                ["Info"] = "情報",
                ["Crop"] = "切り取り",
                ["Rotate"] = "回転",
                ["FlipH"] = "水平反転",
                ["FlipV"] = "垂直反転",
                ["PixelSizeLabel"] = "ピクセルサイズ：",
                ["AddColor"] = "色を追加",
                ["ShapeType"] = "図形タイプ",
                ["LineStyle"] = "線スタイル",
                ["Outline"] = "アウトライン",
                ["Style"] = "スタイル",
                ["Bold"] = "太字",
                ["Italic"] = "斜体",
                ["Underline"] = "下線",
                ["Shadow"] = "影",
                
                // ステッカー/ガイド
                ["CopiedToClipboard"] = "クリップボードにコピーしました",
                ["Captured"] = "キャプチャしました。",
                ["ClickWindowThenEnter"] = "キャプチャするウィンドウをクリックして Enter を押してください",
                ["PressEnterToStartScroll"] = "Enter を押してスクロールキャプチャを開始",
                ["WindowNotFound"] = "キャプチャするウィンドウが見つかりません",
                ["StartingScrollCapture"] = "スクロールキャプチャを開始します...",
                ["NoScrollInWindow"] = "スクロール可能な領域がありません",
                ["NoScrollableContent"] = "スクロール可能なコンテンツがありません",
                ["ScrollCompletedDuplicate"] = "スクロール完了（重複検出）",
                ["ReachedMaxCaptures"] = "最大キャプチャ数に達しました",
                ["ReachedMaxScrolls"] = "最大スクロール回数に達しました",
                ["RealTimeF1Guide"] = "目的の画面を表示して [F1] を押してください\n（キャンセル：ESC）",
                ["MultiCaptureHud"] = "ENTER：合成\nF1：個別保存\nESC：終了",
                ["ScrollClickToStart"] = "クリックしてスクロールキャプチャ開始",
                ["EscToCancel"] = "中止",
                
                // Bottom actions & confirmations
                ["AllCopiedToClipboard"] = "すべてクリップボードにコピーしました",
                ["AllImagesSaved"] = "すべての画像を保存しました",
                ["UnsavedImageDeleteConfirm"] = "未保存の画像を削除しますか？",
                ["UnsavedImagesDeleteConfirm"] = "未保存の画像が含まれています。すべて削除しますか？",
                ["ClipboardCopyFailed"] = "クリップボードへのコピーに失敗しました",
                ["CopyError"] = "コピーエラー",
                
                // Tooltips & common
                ["Minimize"] = "最小化",
                ["Close"] = "閉じる",
                ["NoDelay"] = "遅延なし",
                
                // Print preview
                ["PrintPreviewTitle"] = "印刷プレビュー",
                ["OrientationLabel"] = "方向:",
                ["Portrait"] = "縦",
                ["Landscape"] = "横",
                ["PrintOptionsLabel"] = "印刷オプション:",
                ["FitToPage"] = "ページに合わせる",
                ["ActualSize"] = "実際のサイズ",
                ["FillPage"] = "ページに合わせて拡大",
                ["Print"] = "印刷",
                ["PreviewGenerationError"] = "プレビュー生成中のエラー",
                ["PrintingStarted"] = "印刷を開始しました。",
                ["PrintingError"] = "印刷中のエラー",
                ["NoImageToPrint"] = "印刷できる画像がありません。",
                ["PrintPreviewError"] = "印刷プレビュー中にエラーが発生しました",
                ["RecaptureError"] = "再キャプチャ中にエラーが発生しました",
                
                // Settings window extras
                ["Version"] = "バージョン",
                ["VisitHomepage"] = "ホームページに移動",
                ["RestoreDefaults"] = "デフォルトに復元",
                ["PrivacyPolicy"] = "プライバシーポリシー",
                ["StartupModeNotice"] = "※ 起動モードは、プログラム起動時に最初に表示される画面を設定します。",
                
                // PreviewWindow extras
                ["Recapture"] = "再キャプチャ",
                
                // MainWindow: Open dialog / errors / topmost
                ["OpenImageTitle"] = "画像を開く",
                ["ImageFilesFilter"] = "画像ファイル",
                ["AllFiles"] = "すべてのファイル",
                ["FileOpenErrorTitle"] = "エラー",
                ["FileOpenErrorPrefix"] = "ファイルを開けませんでした",
                ["TopmostOnMsg"] = "最前面固定：オン",
                ["TopmostOffMsg"] = "最前面固定：オフ",
            };

            // 영어
            _translations["en"] = new Dictionary<string, string>
            {
                // Main menu (no 'Capture')
                ["AreaCapture"] = "Area",
                ["DelayCapture"] = "Delay",
                ["RealTimeCapture"] = "Instant",
                ["MultiCapture"] = "Multi",
                ["FullScreen"] = "Full Screen",
                ["DesignatedCapture"] = "Designated",
                ["WindowCapture"] = "Window",
                ["ElementCapture"] = "Element",
                ["ScrollCapture"] = "Scroll",
                
                // Settings common
                ["Settings"] = "Settings",
                ["SystemSettings"] = "System Settings",
                ["CaptureSettings"] = "Capture Settings",
                ["HotkeySettings"] = "Hotkey Settings",
                ["Language"] = "Language",
                ["StartWithWindows"] = "Start with Windows",
                ["StartupMode"] = "Startup Mode",
                ["Normal"] = "Normal",
                ["Simple"] = "Simple",
                ["Tray"] = "Tray",
                ["General"] = "General",
                ["SaveSettings"] = "Save Settings",
                ["SavePath"] = "Save Path",
                ["Change"] = "Change",
                ["FileFormat"] = "File Format",
                ["Quality"] = "Quality",
                ["Options"] = "Options",
                ["AutoSaveCapture"] = "Auto save on capture",
                ["ShowPreviewAfterCapture"] = "Show preview/edit after capture",
                ["UsePrintScreen"] = "Use Print Screen key",
                ["StartInTray"] = "Start in Tray Mode",
                ["StartInNormal"] = "Start in Normal Mode",
                ["StartInSimple"] = "Start in Simple Mode",
                ["LanguageSettings"] = "Language Settings",
                ["LanguageLabel"] = "Language",
                ["OpenSettings"] = "Open Settings",
                ["InstantEdit"] = "Instant Edit",
                
                // Buttons
                ["Save"] = "Save",
                ["Cancel"] = "Cancel",
                ["OK"] = "OK",
                ["Apply"] = "Apply",
                ["Close"] = "Close",
                ["CopySelected"] = "Copy",
                ["CopyAll"] = "Copy All",
                ["SaveAll"] = "Save All",
                ["Delete"] = "Delete",
                ["DeleteAll"] = "Delete All",
                
                // Tray menu
                ["TrayNormalMode"] = "Normal Mode",
                ["TraySimpleMode"] = "Simple Mode",
                ["TrayTrayMode"] = "Tray Mode",
                ["Exit"] = "Exit",
                ["AppName"] = "CatchCapture",
                
                // Messages
                ["SettingsSaved"] = "Settings saved successfully",
                ["SettingsApplied"] = "Settings applied successfully",
                ["SettingsSaveFailed"] = "Failed to save settings",
                ["RestartRequired"] = "Please restart the program to apply language changes",
                
                // Extras
                ["Delay3Sec"] = "Capture after 3s",
                ["Delay5Sec"] = "Capture after 5s",
                ["Delay10Sec"] = "Capture after 10s",
                ["AddIcon"] = "Add Icon",
                ["DeleteIcon"] = "Delete Icon",
                
                // Preview/Image Edit window toolbar, tooltips, dialogs, and labels
                ["ImageEditTitle"] = "Image Edit",
                ["CopyToClipboard"] = "Copy to Clipboard",
                ["UndoLbl"] = "Undo",
                ["RedoLbl"] = "Redo",
                ["ResetLbl"] = "Reset",
                ["CropLbl"] = "Crop",
                ["RotateLbl"] = "Rotate",
                ["FlipHLbl"] = "Flip H",
                ["FlipVLbl"] = "Flip V",
                ["ShapeLbl"] = "Shape",
                ["ShapeOptions"] = "Shape Options",
                ["ToolOptions"] = "Tool Options",
                ["OCR"] = "OCR",
                ["Extract"] = "Extract",
                ["ZoomOut"] = "Zoom Out",
                ["ZoomIn"] = "Zoom In",
                ["ZoomReset"] = "Actual",
                ["ZoomResetTooltip"] = "100%",
                ["RecentCaptures"] = "Recent Captures",
                ["SizeLabel"] = "Size",
                ["ImageSizePrefix"] = "Image size: ",
                ["ImageSaved"] = "Image has been saved.",
                ["ImageSaveTitle"] = "Save Image",
                ["NoExtractedText"] = "No text extracted.",
                ["OcrResultTitle"] = "OCR Result",
                ["ExtractedText"] = "Extracted Text",
                ["Info"] = "Info",
                ["Error"] = "Error",
                ["Confirm"] = "Confirm",
                ["ConfirmReset"] = "Do you want to discard all edits and revert to the original image?",
                
                // SnippingWindow
                ["SelectAreaGuide"] = "Select an area (Press ESC to cancel)",
                ["SelectionTooSmall"] = "Selected area is too small. Please select again.",
                ["Color"] = "Color",
                ["TextOptions"] = "Text Options",
                ["Font"] = "Font",
                ["Thickness"] = "Thickness",
                ["Fill"] = "Fill",
                ["FillOpacity"] = "Fill Opacity",
                ["Intensity"] = "Intensity",
                ["NoImageForOcr"] = "No image available for OCR.",
                ["OcrError"] = "Error during text extraction",
                ["Copied"] = "Copied",
                ["NoImageToSave"] = "No image to save.",
                ["SaveError"] = "Error saving file",
                
                // 이미지 검색 (PreviewWindow)
                ["ImageSearch"] = "Search",
                ["ImageSearchTooltip"] = "Search by image",
                
                // 편집 도구
                ["Pen"] = "Pen",
                ["Highlighter"] = "Highlighter",
                ["Text"] = "Text",
                ["TextAdd"] = "Add Text",
                ["Mosaic"] = "Mosaic",
                ["Eraser"] = "Eraser",
                ["Numbering"] = "Numbering",
                
                // Common UI
                ["OK"] = "OK",
                ["Cancel"] = "Cancel",
                ["Error"] = "Error",
                ["Info"] = "Info",
                ["Crop"] = "Crop",
                ["Rotate"] = "Rotate",
                ["FlipH"] = "FlipH",
                ["FlipV"] = "FlipV",
                ["PixelSizeLabel"] = "Pixel size:",
                ["AddColor"] = "Add color",
                ["ShapeType"] = "Shape type",
                ["LineStyle"] = "Line style",
                ["Outline"] = "Outline",
                ["Style"] = "Style",
                ["Bold"] = "Bold",
                ["Italic"] = "Italic",
                ["Underline"] = "Underline",
                ["Shadow"] = "Shadow",
                
                // Stickers / Guide
                ["CopiedToClipboard"] = "Copied to clipboard",
                ["Captured"] = "Captured.",
                ["ClickWindowThenEnter"] = "Click the window to capture, then press Enter",
                ["PressEnterToStartScroll"] = "Press Enter to start scroll capture",
                ["WindowNotFound"] = "Could not find a window to capture",
                ["StartingScrollCapture"] = "Starting scroll capture...",
                ["NoScrollInWindow"] = "This window has no scroll",
                ["NoScrollableContent"] = "No scrollable content",
                ["ScrollCompletedDuplicate"] = "Scroll completed (duplicate detected)",
                ["ReachedMaxCaptures"] = "Reached maximum number of captures",
                ["ReachedMaxScrolls"] = "Reached maximum number of scrolls",
                ["RealTimeF1Guide"] = "Show the target screen and press [F1]\n(Cancel: ESC)",
                ["MultiCaptureHud"] = "ENTER: Merge\nF1: Save individually\nESC: Exit",
                ["ScrollClickToStart"] = "Click to start scroll capture",
                ["EscToCancel"] = "Cancel",
                
                // Bottom actions & confirmations
                ["AllCopiedToClipboard"] = "All items copied to clipboard",
                ["AllImagesSaved"] = "All images have been saved",
                ["UnsavedImageDeleteConfirm"] = "Delete the unsaved image?",
                ["UnsavedImagesDeleteConfirm"] = "There are unsaved images. Delete all?",
                ["ClipboardCopyFailed"] = "Failed to copy to clipboard",
                ["CopyError"] = "Copy error",
                
                // Tooltips & common
                ["Minimize"] = "Minimize",
                ["Close"] = "Close",
                ["NoDelay"] = "No delay",
                
                // Print preview
                ["PrintPreviewTitle"] = "Print Preview",
                ["OrientationLabel"] = "Orientation:",
                ["Portrait"] = "Portrait",
                ["Landscape"] = "Landscape",
                ["PrintOptionsLabel"] = "Print Options:",
                ["FitToPage"] = "Fit to Page",
                ["ActualSize"] = "Actual Size",
                ["FillPage"] = "Fill Page",
                ["Print"] = "Print",
                ["PreviewGenerationError"] = "Error generating preview",
                ["PrintingStarted"] = "Printing has started.",
                ["PrintingError"] = "Error while printing",
                ["NoImageToPrint"] = "There is no image to print.",
                ["PrintPreviewError"] = "An error occurred during print preview",
                ["RecaptureError"] = "An error occurred during recapture",
                
                // Settings window extras
                ["Version"] = "Version",
                ["VisitHomepage"] = "Visit Homepage",
                ["RestoreDefaults"] = "Restore Defaults",
                ["PrivacyPolicy"] = "Privacy Policy",
                ["StartupModeNotice"] = "※ Startup mode determines which screen appears first when the app launches.",
                
                // PreviewWindow extras
                ["Recapture"] = "Capture",
                
                // MainWindow: Open dialog / errors / topmost
                ["OpenImageTitle"] = "Open Image",
                ["ImageFilesFilter"] = "Image Files",
                ["AllFiles"] = "All Files",
                ["FileOpenErrorTitle"] = "Error",
                ["FileOpenErrorPrefix"] = "Could not open file",
                ["TopmostOnMsg"] = "Pinned to top: On",
                ["TopmostOffMsg"] = "Pinned to top: Off",
            };

            // 스페인어
            _translations["es"] = new Dictionary<string, string>(_translations["en"])
            {
                ["Settings"] = "Configuración",
                ["General"] = "General",
                ["CaptureSettings"] = "Ajustes de captura",
                ["SystemSettings"] = "Ajustes del sistema",
                ["HotkeySettings"] = "Atajos de teclado",
                
                ["Save"] = "Guardar",
                ["Print"] = "Imprimir",
                ["CopySelected"] = "Copiar",
                ["CopyToClipboard"] = "Copiado al portapapeles",
                ["ImageSaveTitle"] = "Guardar imagen",
                ["ImageSaved"] = "Imagen guardada",
                ["Error"] = "Error",
                ["Info"] = "Información",
                
                ["UndoLbl"] = "Deshacer",
                ["RedoLbl"] = "Rehacer",
                ["ResetLbl"] = "Restablecer",
                ["Crop"] = "Recortar",
                ["Rotate"] = "Girar",
                ["FlipH"] = "Volteo H",
                ["FlipV"] = "Volteo V",
                
                ["Pen"] = "Pluma",
                ["Highlighter"] = "Resaltador",
                ["Text"] = "Texto",
                ["TextAdd"] = "Añadir texto",
                ["Mosaic"] = "Mosaico",
                ["Eraser"] = "Borrador",
                
                ["ImageSearch"] = "Buscar imagen",
                ["ImageSearchTooltip"] = "Buscar por imagen",
                ["OCR"] = "OCR",
                ["Extract"] = "Extraer",
                
                ["ZoomReset"] = "Original",
                ["ZoomResetTooltip"] = "100%",
                ["ZoomOut"] = "Alejar",
                ["ZoomIn"] = "Acercar",
                ["ImageSizePrefix"] = "Tamaño: ",
                ["RecentCaptures"] = "Capturas recientes",
                ["SizeLabel"] = "Tamaño",
                
                ["StartupMode"] = "Modo de inicio",
                ["StartWithWindows"] = "Iniciar con Windows",
                ["StartInTray"] = "Iniciar en bandeja",
                ["StartInNormal"] = "Iniciar en modo normal",
                ["StartInSimple"] = "Iniciar en modo simple",
                ["StartupModeNotice"] = "※ El modo de inicio determina la pantalla que aparece primero al iniciar la app.",
                
                ["LanguageSettings"] = "Idioma",
                ["LanguageLabel"] = "Idioma",
                ["RestartRequired"] = "Se requiere reiniciar la aplicación tras cambiar el idioma.",
                
                ["Version"] = "Versión",
                ["VisitHomepage"] = "Visitar sitio web",
                ["RestoreDefaults"] = "Restaurar por defecto",
                ["PrivacyPolicy"] = "Política de privacidad",
                
                ["Recapture"] = "Capturar",
                ["NoImageToPrint"] = "No hay imagen para imprimir.",
                ["PrintPreviewError"] = "Se produjo un error durante la vista previa de impresión",
                ["RecaptureError"] = "Se produjo un error durante la recaptura",
                
                ["OpenImageTitle"] = "Abrir imagen",
                ["ImageFilesFilter"] = "Archivos de imagen",
                ["AllFiles"] = "Todos los archivos",
                ["FileOpenErrorTitle"] = "Error",
                ["FileOpenErrorPrefix"] = "No se puede abrir el archivo",
                ["TopmostOnMsg"] = "Fijado arriba: Activado",
                ["TopmostOffMsg"] = "Fijado arriba: Desactivado",
            };

            // 독일어
            _translations["de"] = new Dictionary<string, string>(_translations["en"])
            {
                ["Settings"] = "Einstellungen",
                ["General"] = "Allgemein",
                ["CaptureSettings"] = "Aufnahme-Einstellungen",
                ["SystemSettings"] = "Systemeinstellungen",
                ["HotkeySettings"] = "Tastenkürzel",
                
                ["Save"] = "Speichern",
                ["Print"] = "Drucken",
                ["CopySelected"] = "Kopieren",
                ["CopyToClipboard"] = "In die Zwischenablage kopiert",
                ["ImageSaveTitle"] = "Bild speichern",
                ["ImageSaved"] = "Bild gespeichert",
                ["Error"] = "Fehler",
                ["Info"] = "Info",
                
                ["UndoLbl"] = "Rückgängig",
                ["RedoLbl"] = "Wiederholen",
                ["ResetLbl"] = "Zurücksetzen",
                ["Crop"] = "Zuschneiden",
                ["Rotate"] = "Drehen",
                ["FlipH"] = "Horizontal spiegeln",
                ["FlipV"] = "Vertikal spiegeln",
                
                ["Pen"] = "Stift",
                ["Highlighter"] = "Textmarker",
                ["Text"] = "Text",
                ["TextAdd"] = "Text hinzufügen",
                ["Mosaic"] = "Mosaik",
                ["Eraser"] = "Radierer",
                
                ["ImageSearch"] = "Bildsuche",
                ["ImageSearchTooltip"] = "Nach Bild suchen",
                ["OCR"] = "OCR",
                ["Extract"] = "Extrahieren",
                
                ["ZoomReset"] = "Original",
                ["ZoomResetTooltip"] = "100%",
                ["ZoomOut"] = "Verkleinern",
                ["ZoomIn"] = "Vergrößern",
                ["ImageSizePrefix"] = "Bildgröße: ",
                ["RecentCaptures"] = "Letzte Aufnahmen",
                ["SizeLabel"] = "Größe",
                
                ["StartupMode"] = "Startmodus",
                ["StartWithWindows"] = "Mit Windows starten",
                ["StartInTray"] = "Im Tray starten",
                ["StartInNormal"] = "Im normalen Modus starten",
                ["StartInSimple"] = "Im einfachen Modus starten",
                ["StartupModeNotice"] = "※ Der Startmodus bestimmt den ersten Bildschirm beim Start der App.",
                
                ["LanguageSettings"] = "Sprache",
                ["LanguageLabel"] = "Sprache",
                ["RestartRequired"] = "Neustart nach Sprachänderung erforderlich.",
                
                ["Version"] = "Version",
                ["VisitHomepage"] = "Webseite besuchen",
                ["RestoreDefaults"] = "Auf Standard zurücksetzen",
                ["PrivacyPolicy"] = "Datenschutzerklärung",
                
                ["Recapture"] = "Aufnehmen",
                ["NoImageToPrint"] = "Kein Bild zum Drucken vorhanden.",
                ["PrintPreviewError"] = "Beim Druckvorschau ist ein Fehler aufgetreten",
                ["RecaptureError"] = "Beim erneuten Aufnehmen ist ein Fehler aufgetreten",
                
                ["OpenImageTitle"] = "Bild öffnen",
                ["ImageFilesFilter"] = "Bilddateien",
                ["AllFiles"] = "Alle Dateien",
                ["FileOpenErrorTitle"] = "Fehler",
                ["FileOpenErrorPrefix"] = "Datei konnte nicht geöffnet werden",
                ["TopmostOnMsg"] = "Immer im Vordergrund: Ein",
                ["TopmostOffMsg"] = "Immer im Vordergrund: Aus",
            };

            // 프랑스어
            _translations["fr"] = new Dictionary<string, string>(_translations["en"])
            {
                ["Settings"] = "Paramètres",
                ["General"] = "Général",
                ["CaptureSettings"] = "Paramètres de capture",
                ["SystemSettings"] = "Paramètres système",
                ["HotkeySettings"] = "Raccourcis clavier",
                
                ["Save"] = "Enregistrer",
                ["Print"] = "Imprimer",
                ["CopySelected"] = "Copier",
                ["CopyToClipboard"] = "Copié dans le presse-papiers",
                ["ImageSaveTitle"] = "Enregistrer l'image",
                ["ImageSaved"] = "Image enregistrée",
                ["Error"] = "Erreur",
                ["Info"] = "Info",
                
                ["UndoLbl"] = "Annuler",
                ["RedoLbl"] = "Rétablir",
                ["ResetLbl"] = "Réinitialiser",
                ["Crop"] = "Rogner",
                ["Rotate"] = "Pivoter",
                ["FlipH"] = "Miroir H",
                ["FlipV"] = "Miroir V",
                
                ["Pen"] = "Stylo",
                ["Highlighter"] = "Surligneur",
                ["Text"] = "Texte",
                ["TextAdd"] = "Ajouter du texte",
                ["Mosaic"] = "Mosaïque",
                ["Eraser"] = "Gomme",
                
                ["ImageSearch"] = "Recherche d'image",
                ["ImageSearchTooltip"] = "Rechercher par image",
                ["OCR"] = "OCR",
                ["Extract"] = "Extraire",
                
                ["ZoomReset"] = "Original",
                ["ZoomResetTooltip"] = "100%",
                ["ZoomOut"] = "Dézoomer",
                ["ZoomIn"] = "Zoomer",
                ["ImageSizePrefix"] = "Taille: ",
                ["RecentCaptures"] = "Captures récentes",
                ["SizeLabel"] = "Taille",
                
                ["StartupMode"] = "Mode de démarrage",
                ["StartWithWindows"] = "Démarrer avec Windows",
                ["StartInTray"] = "Démarrer dans la zone de notification",
                ["StartInNormal"] = "Démarrer en mode normal",
                ["StartInSimple"] = "Démarrer en mode simple",
                ["StartupModeNotice"] = "※ Le mode de démarrage détermine l'écran affiché au lancement de l'application.",
                
                ["LanguageSettings"] = "Langue",
                ["LanguageLabel"] = "Langue",
                ["RestartRequired"] = "Un redémarrage est requis après modification de la langue.",
                
                ["Version"] = "Version",
                ["VisitHomepage"] = "Visiter le site",
                ["RestoreDefaults"] = "Restaurer par défaut",
                ["PrivacyPolicy"] = "Politique de confidentialité",
                
                ["Recapture"] = "Capturer",
                ["NoImageToPrint"] = "Aucune image à imprimer.",
                ["PrintPreviewError"] = "Une erreur s'est produite lors de l'aperçu avant impression",
                ["RecaptureError"] = "Une erreur s'est produite lors de la recapture",
                
                ["OpenImageTitle"] = "Ouvrir l'image",
                ["ImageFilesFilter"] = "Fichiers image",
                ["AllFiles"] = "Tous les fichiers",
                ["FileOpenErrorTitle"] = "Erreur",
                ["FileOpenErrorPrefix"] = "Impossible d'ouvrir le fichier",
                ["TopmostOnMsg"] = "Épinglé en haut : Activé",
                ["TopmostOffMsg"] = "Épinglé en haut : Désactivé",
            };
        }
    }
}

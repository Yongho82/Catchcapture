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
                
                // Stickers / Guide messages
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
                ["RealTimeF1Guide"] = "目的の画面を表示して [F1] を押してください\n（キャンセル: ESC）",
                ["MultiCaptureHud"] = "ENTER：合成\nF1：個別保存\nESC：終了",
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
                ["Info"] = "Info",
                ["Error"] = "Error",
                ["Confirm"] = "Confirm",
                ["ConfirmReset"] = "Discard all edits and revert to original image?",
                
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
                ["ImageSearch"] = "Imag Search",
                ["ImageSearchTooltip"] = "Search by image",
                
                // 편집 도구
                ["Pen"] = "Pen",
                ["Highlighter"] = "Highlighter",
                ["Text"] = "Text",
                ["TextAdd"] = "Add Text",
                ["Mosaic"] = "Mosaic",
                ["Eraser"] = "Eraser",
                
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
            };
        }
    }
}

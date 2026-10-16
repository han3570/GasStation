using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GasStation
{
    public class GameUIController : MonoBehaviour
    {
        readonly Color ink = Hex("0C2222"), panel = Hex("17322F"), line = Hex("304B42"), cream = Hex("F3EFDF"), muted = Hex("92AA99"), amber = Hex("FFBB55"), mint = Hex("75DCAA"), red = Hex("F27E65");
        Font font;
        GameSessionManager session;
        Canvas canvas;
        RectTransform board;
        Text operating, score, count, carName, resistance, fuel, target, carTime, flow, pressureValue, status, instruction, feedback, best, balance, resultScore, resultStats, resultExport, debugStats;
        Image fuelFill, targetBand, vehicleTimeFill, holdBackground, feedbackPanel;
        RectTransform targetMarker;
        PrototypeGraphic station, gauge;
        GameObject startPanel, resultsPanel, pausePanel, debugPanel;
        Button holdButton;
        Text soundLabel, fuelInfo, volumeStats, seedLabel, holdLabel, resultNote;
        Text lossHistory, failurePenalty, failureDetails, resultLoss, residualHint;
        Text operatingLabel;
        Text priceInfo, totalProfit, lastSale, resultRevenue, resultCost, resultProfit, transactionSale;
        GameObject failurePanel;
        GameObject successPanel;
        CanvasGroup successOpacity;
        PrototypeGraphic successSparks;
        Text successTitle, successPoints, successBonuses;
        FuelingRecord celebratedRecord;
        double celebrationStart, celebrationDuration;
        int previousWidth, previousHeight;
        Rect previousSafeArea;

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out var c); return c; }
        public void Build(GameSessionManager manager)
        {
            session = manager;
            font = Resources.Load<Font>("NotoSansKR");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Pressure Station UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            board = new GameObject("Portrait Board", typeof(RectTransform)).GetComponent<RectTransform>();
            board.SetParent(root.transform,false); board.anchorMin = board.anchorMax = board.pivot = new Vector2(.5f,.5f); board.sizeDelta = new Vector2(540,1170);
            Box(board,"Background",0,0,540,1170,ink);
            // Quiet orange masthead and compact operational dashboard.
            Box(board,"Brand",24,31,42,42,amber); Label(board,"P",24,29,42,42,29,ink,TextAnchor.MiddleCenter,true);
            Label(board,"PRESSURE STATION",79,30,335,27,23,cream,TextAnchor.MiddleLeft,true);
            priceInfo=Label(board,"",80,62,340,18,11,muted);
            var sound=Button(board,"♪",436,33,34,34,panel,cream,ToggleSound); soundLabel=sound.GetComponentInChildren<Text>();
            Button(board,"Ⅱ",482,33,34,34,panel,cream,session.TogglePause);
            Box(board,"Dashboard",24,108,492,100,panel);
            Box(board,"Divide",200,127,1,64,line); Box(board,"Divide",356,127,1,64,line);
            operatingLabel=Label(board,"남은 영업시간",40,120,148,20,12,muted);
            operating=Label(board,"03:00",40,144,148,48,36,cream,TextAnchor.MiddleLeft,true);
            Label(board,"처리 차량",218,120,124,20,12,muted);
            count=Label(board,"00",218,147,120,43,32,cream,TextAnchor.MiddleLeft,true);
            Label(board,"총 점수",374,120,127,20,12,muted);
            score=Label(board,"0",374,147,127,43,32,amber,TextAnchor.MiddleLeft,true);
            lossHistory=Label(board,"",24,210,492,18,11,red,TextAnchor.MiddleRight);
            Box(board,"Sales Dashboard",24,230,492,36,panel);
            totalProfit=Label(board,"",34,232,245,31,14,mint,TextAnchor.MiddleLeft,true);
            lastSale=Label(board,"",284,232,222,31,11,cream,TextAnchor.MiddleRight);
            station=Art(board,24,274,492,234,PrototypeGraphic.Illustration.Station);
            Label(board,"SELF SERVICE",42,283,160,17,10,cream,TextAnchor.MiddleLeft,true);
            Label(board,"01 / SINGLE PUMP",331,283,165,17,10,cream,TextAnchor.MiddleRight);
            Box(board,"BayLight",42,475,6,6,mint);
            status=Label(board,"주유 준비 완료",56,466,410,26,11,muted);
            carName=Label(board,"경차",24,525,200,39,28,cream,TextAnchor.MiddleLeft,true);
            resistance=Label(board,"차량 저항 20",280,533,236,30,15,muted,TextAnchor.MiddleRight);
            fuelInfo=Label(board,"",24,557,492,18,11,muted);
            Box(board,"Rule",24,577,492,1,line);
            Label(board,"현재 주유량",24,592,200,22,12,muted);
            fuel=Label(board,"0.00",24,618,270,65,51,cream,TextAnchor.MiddleLeft,true);
            Label(board,"L",257,648,35,29,20,muted);
            target=Label(board,"목표 35 L\n허용 범위 30–40 L",307,620,209,60,16,cream,TextAnchor.MiddleRight);
            Box(board,"Fuel Track",24,700,492,16,line);
            targetBand=Box(board,"Acceptable Range",24,694,70,28,new Color(mint.r,mint.g,mint.b,.25f));
            fuelFill=Box(board,"Fuel Fill",24,700,0,16,Hex("7AB7CB"));
            targetMarker=Box(board,"Target",24,690,2,36,mint).rectTransform;
            Label(board,"0 L",24,731,130,18,10,muted);
            Label(board,"초록 구간에 멈추세요",293,731,223,18,10,mint,TextAnchor.MiddleRight);
            carTime=Label(board,"차량 제한시간   12.0 s",24,769,390,22,12,muted);
            vehicleTimeFill=Box(board,"Vehicle Time",24,804,492,3,mint);
            gauge=Art(board,24,826,240,210,PrototypeGraphic.Illustration.Gauge);
            Label(board,"PRESSURE",24,884,240,20,10,muted,TextAnchor.MiddleCenter);
            pressureValue=Label(board,"0",24,904,240,55,42,cream,TextAnchor.MiddleCenter,true);
            Label(board,"0",49,1006,40,20,11,muted); Label(board,"100",201,1006,42,20,11,muted,TextAnchor.MiddleRight);
            Label(board,"현재 유량",302,858,204,23,12,muted);
            flow=Label(board,"0.0 L/s",302,887,211,42,29,cream,TextAnchor.MiddleLeft,true);
            Box(board,"Threshold Dot",302,952,6,6,mint);
            Label(board,"초록 눈금 = 차량 저항",316,941,201,27,11,mint);
            residualHint=Label(board,"손을 떼도 남은 압력으로\n주유가 계속됩니다.",302,977,204,48,12,muted);
            holdButton=Button(board,"누르고 있으면 압력 상승",24,1047,492,79,amber,ink,null,23);
            holdBackground=holdButton.GetComponent<Image>();
            holdLabel=holdButton.GetComponentInChildren<Text>();
            holdButton.gameObject.AddComponent<HoldPressureButton>().session=session;
            instruction=Label(board,"마우스 · 터치 · 스페이스 키를 길게 누르세요",24,1139,440,18,11,muted,TextAnchor.MiddleCenter);
            Button(board,"F1",482,1133,34,28,panel,muted,session.ToggleDebug,12);
            feedbackPanel=Box(board,"Feedback",24,510,492,66,mint);
            feedback=Label(feedbackPanel.transform,"Feedback Text",8,0,476,66,22,ink,TextAnchor.MiddleCenter,true);
            feedbackPanel.gameObject.SetActive(false);
            var failureBorder=Box(board,"Overfill Failure",24,264,492,194,red);
            failurePanel=failureBorder.gameObject;
            Box(failurePanel.transform,"Failure Card",4,4,484,186,Hex("492C29"));
            Label(failurePanel.transform,"주유 실패 · 초과 주유",16,14,460,36,26,cream,TextAnchor.MiddleCenter,true);
            failurePenalty=Label(failurePanel.transform,"−80점",16,52,460,64,46,red,TextAnchor.MiddleCenter,true);
            failureDetails=Label(failurePanel.transform,"",16,125,460,49,14,cream,TextAnchor.MiddleCenter);
            failurePanel.SetActive(false);
            var saleCard=Box(board,"Transaction Sale",24,748,492,53,panel);
            transactionSale=Label(saleCard.transform,"",10,1,472,51,12,cream,TextAnchor.MiddleCenter);
            saleCard.gameObject.SetActive(false);
            successPanel=Rect(board,"Success Celebration",24,264,492,178).gameObject;
            successOpacity=successPanel.AddComponent<CanvasGroup>();
            successOpacity.interactable=false; successOpacity.blocksRaycasts=false;
            successSparks=Art(successPanel.transform,0,0,492,178,PrototypeGraphic.Illustration.Celebration);
            Box(successPanel.transform,"Success Badge",78,16,336,142,new Color(ink.r,ink.g,ink.b,.94f));
            successTitle=Label(successPanel.transform,"주유 성공!",94,25,304,32,23,mint,TextAnchor.MiddleCenter,true);
            successPoints=Label(successPanel.transform,"+0점",94,61,304,53,36,cream,TextAnchor.MiddleCenter,true);
            successBonuses=Label(successPanel.transform,"",88,116,316,30,12,mint,TextAnchor.MiddleCenter);
            successPanel.SetActive(false);
            session.Simulation.Resolved+=CelebrateSuccess;
            BuildStart(); BuildResults(); BuildPause(); BuildDebug();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            Resize(); Refresh();
        }

        void BuildStart()
        {
            startPanel=Box(board,"Start Screen",0,0,540,1170,ink).gameObject;
            var p=startPanel.transform;
            Box(p,"Brand",24,37,42,42,amber); Label(p,"P",24,35,42,42,29,ink,TextAnchor.MiddleCenter,true);
            Label(p,"PRESSURE STATION",80,37,410,27,24,cream,TextAnchor.MiddleLeft,true);
            Label(p,"압력 주유 프로토타입  /  v0.2.6",80,69,410,21,11,muted);
            Label(p,"한 번의 터치.\n마지막 한 방울.",24,135,492,137,49,cream,TextAnchor.MiddleLeft,true);
            Label(p,"차량마다 다른 목표량, 정확하게 멈추세요.\n점수와 함께 주유 이익도 쌓입니다.",24,292,492,63,17,muted);
            Art(p,24,386,492,280,PrototypeGraphic.Illustration.Station);
            Label(p,"OPEN  /  READY FOR YOUR FIRST SHIFT",42,395,440,22,11,cream);
            string[] titles={"누르고", "놓고", "기다리세요"};
            string[] descriptions={"압력을 올려 주유", "목표보다 일찍 감압", "흐름이 멈추면 판정"};
            for(int i=0;i<3;i++)
            {
                float x=24+i*169;
                Label(p,"0"+(i+1),x,709,154,29,20,amber,TextAnchor.MiddleLeft,true);
                Label(p,titles[i],x,747,154,29,20,cream,TextAnchor.MiddleLeft,true);
                Label(p,descriptions[i],x,789,154,22,11,muted);
            }
            Box(p,"Rule",24,841,492,1,line);
            Label(p,"허용 상한을 넘으면 즉시 실패 · −80점",24,819,492,20,12,red,TextAnchor.MiddleCenter);
            Label(p,"감압 속도 선택",24,862,492,24,12,muted);
            Button(p,"기본",24,902,156,47,panel,cream,()=>session.SelectBalance(0),15);
            Button(p,"빠르게",192,902,156,47,panel,cream,()=>session.SelectBalance(1),15);
            Button(p,"느리게",360,902,156,47,panel,cream,()=>session.SelectBalance(2),15);
            balance=Label(p,"STANDARD · 28 / s",24,959,492,24,11,mint,TextAnchor.MiddleCenter);
            Button(p,"영업 시작  →",24,1003,492,79,amber,ink,session.StartSession,23);
            best=Label(p,"최고 기록 0   ·   마우스 / 터치 / 스페이스",24,1101,492,25,12,muted,TextAnchor.MiddleCenter);
            Button(p,"테스트 설정",192,1134,156,26,ink,muted,session.ToggleDebug,11);
        }
        void BuildResults()
        {
            resultsPanel=Box(board,"Results Screen",0,0,540,1170,ink).gameObject;
            var p=resultsPanel.transform;
            Label(p,"SHIFT COMPLETE",24,73,492,30,14,amber,TextAnchor.MiddleCenter,true);
            Label(p,"오늘의 영업 종료",24,125,492,59,36,cream,TextAnchor.MiddleCenter,true);
            Label(p,"최종 점수",24,223,492,28,14,muted,TextAnchor.MiddleCenter);
            resultScore=Label(p,"0",24,264,492,102,76,amber,TextAnchor.MiddleCenter,true);
            resultLoss=Label(p,"",24,369,492,46,14,red,TextAnchor.MiddleCenter,true);
            Box(p,"Result Card",24,419,492,401,panel);
            Label(p,"매출",40,432,140,22,12,muted);
            Label(p,"연료 원가",205,432,140,22,12,muted);
            Label(p,"주유 이익",370,432,130,22,12,mint);
            resultRevenue=Label(p,"",40,459,145,35,18,cream,TextAnchor.MiddleLeft,true);
            resultCost=Label(p,"",205,459,145,35,18,cream,TextAnchor.MiddleLeft,true);
            resultProfit=Label(p,"",370,459,130,35,18,mint,TextAnchor.MiddleLeft,true);
            Label(p,"매출 − 연료 원가 · 점수 보너스는 금액과 별도",40,501,460,22,11,muted);
            Box(p,"Sales Divider",40,537,460,1,line);
            resultStats=Label(p,"",45,553,450,252,17,cream);
            resultStats.lineSpacing=1.3f;
            volumeStats=Label(p,"",24,835,492,44,12,mint,TextAnchor.MiddleCenter);
            resultNote=Label(p,"",24,883,492,22,11,muted,TextAnchor.MiddleCenter);
            Button(p,"다시 영업하기  →",24,933,492,76,amber,ink,session.StartSession,23);
            Button(p,"차량별 기록 CSV 저장",24,1026,492,52,panel,cream,session.ExportResults,15);
            resultExport=Label(p,"",24,1096,492,48,10,muted,TextAnchor.MiddleCenter);
            Button(p,"테스트 설정",192,1140,156,26,ink,muted,session.ToggleDebug,11);
        }
        void BuildPause()
        {
            pausePanel=Box(board,"Pause Overlay",0,0,540,1170,new Color(ink.r,ink.g,ink.b,.96f)).gameObject;
            Label(pausePanel.transform,"잠시 쉬어가기",24,386,492,67,38,cream,TextAnchor.MiddleCenter,true);
            Label(pausePanel.transform,"입력을 해제하고 모든 시간을 멈췄습니다.",24,477,492,37,16,muted,TextAnchor.MiddleCenter);
            Button(pausePanel.transform,"영업 계속하기",54,578,432,77,amber,ink,session.TogglePause,22);
            Button(pausePanel.transform,"처음부터 다시 시작",54,677,432,58,panel,cream,session.StartSession,16);
            Label(pausePanel.transform,"ESC · 일시정지 / 계속",24,772,492,25,12,muted,TextAnchor.MiddleCenter);
        }
        void BuildDebug()
        {
            debugPanel=Box(board,"Test Panel",0,0,540,1170,ink).gameObject;
            var p=debugPanel.transform;
            Label(p,"PLAYTEST LAB",24,60,492,33,14,amber,TextAnchor.MiddleLeft,true);
            Label(p,"테스트 설정",24,111,492,65,40,cream,TextAnchor.MiddleLeft,true);
            Label(p,"설정 중에는 게임이 일시정지됩니다.\n예측량은 이 패널에서만 표시됩니다.",24,191,492,65,15,muted);
            Box(p,"Telemetry",24,290,492,200,panel);
            debugStats=Label(p,"",45,307,450,173,14,cream);
            Label(p,"감압 속도",24,521,492,25,14,muted);
            Button(p,"기본 28",24,566,156,48,panel,cream,()=>session.SelectBalance(0),15);
            Button(p,"빠름 40",192,566,156,48,panel,cream,()=>session.SelectBalance(1),15);
            Button(p,"느림 18",360,566,156,48,panel,cream,()=>session.SelectBalance(2),15);
            Label(p,"압력 상승 속도",24,641,235,24,14,muted);
            Button(p,"−10",278,631,111,48,panel,cream,()=>session.AdjustRise(-10),17);
            Button(p,"+10",405,631,111,48,panel,cream,()=>session.AdjustRise(10),17);
            Label(p,"주유 계수",24,705,235,24,14,muted);
            Button(p,"−0.05",278,695,111,48,panel,cream,()=>session.AdjustFlow(-.05f),15);
            Button(p,"+0.05",405,695,111,48,panel,cream,()=>session.AdjustFlow(.05f),15);
            Label(p,"차량 교체 · 현재 차량은 통계에서 제외",24,778,492,24,12,muted);
            Button(p,"경차",24,820,156,48,panel,cream,()=>session.ForceVehicle(0),15);
            Button(p,"승용차",192,820,156,48,panel,cream,()=>session.ForceVehicle(1),15);
            Button(p,"SUV",360,820,156,48,panel,cream,()=>session.ForceVehicle(2),15);
            var seedButton=Button(p,"시드 고정 OFF",24,886,235,44,panel,cream,session.ToggleFixedSeed,13);
            seedLabel=seedButton.GetComponentInChildren<Text>();
            Button(p,"연료 −5 L",278,886,111,44,panel,cream,()=>session.AdjustInitialFuel(-5),12);
            Button(p,"연료 +5 L",405,886,111,44,panel,cream,()=>session.AdjustInitialFuel(5),12);
            Label(p,"연료 변경 시 현재 차량을 초기화합니다.\n수정한 영업은 최고 기록에 반영하지 않습니다.",24,934,492,33,11,muted,TextAnchor.MiddleCenter);
            Button(p,"설정 닫기",24,973,492,75,amber,ink,session.ToggleDebug,22);
            Button(p,"새 영업 시작",24,1071,492,51,panel,cream,()=>{session.ToggleDebug(); session.StartSession();},16);
        }
        void ToggleSound() { session.ToggleSound(); soundLabel.text=session.SoundEnabled?"♪":"×"; }
        void Resize()
        {
            Rect safe=Screen.safeArea;
            if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
            float scale=Mathf.Min(safe.width/540f,safe.height/1170f);
            canvas.scaleFactor=Mathf.Max(.1f,scale);
            board.anchoredPosition=(safe.center-new Vector2(Screen.width,Screen.height)*.5f)/canvas.scaleFactor;
            previousWidth=Screen.width; previousHeight=Screen.height; previousSafeArea=Screen.safeArea;
        }
        public void Refresh()
        {
            if(canvas==null) return;
            if(previousWidth!=Screen.width || previousHeight!=Screen.height || previousSafeArea!=Screen.safeArea) Resize();
            var s=session.Simulation; var v=s.Vehicle; var r=s.Results; var request=s.Request;
            startPanel.SetActive(s.State==SessionState.Ready);
            resultsPanel.SetActive(s.State==SessionState.Result);
            pausePanel.SetActive(s.Paused && !session.DebugVisible);
            debugPanel.SetActive(session.DebugVisible);
            best.text=$"최고 기록 {session.BestScore:N0}   ·   마우스 / 터치 / 스페이스";
            balance.text=$"{session.BalanceName}  ·  {session.settings.pressureDecay:0} / s";
            // Minutes use integer division; the display rounds only the remaining seconds.
            int totalSeconds=(int)Math.Ceiling(s.OperatingRemaining); operating.text=$"{totalSeconds/60:00}:{totalSeconds%60:00}";
            operatingLabel.text=s.OperatingEnded?"영업 마감":"남은 영업시간";
            operating.color=s.OperatingEnded?amber:cream;
            score.text=r.TotalScore.ToString("N0"); count.text=r.Records.Count.ToString("00");
            priceInfo.text=$"판매 {request?.SalePricePerLiter ?? session.settings.salePricePerLiter:N0}원/L · 원가 {request?.FuelCostPerLiter ?? session.settings.fuelCostPerLiter:N0}원/L";
            totalProfit.text=$"누적 이익 {Money(r.TotalProfitWon)}";
            totalProfit.color=r.TotalProfitMilliWon<0?red:mint;
            lastSale.text=s.LastRecord==null?"첫 차량을 기다리고 있어요":$"직전 이익 {Money(s.LastRecord.sale.ProfitWon)}";
            bool saleVisible=s.LastRecord!=null && (s.State==SessionState.Feedback || s.State==SessionState.Arrival);
            transactionSale.transform.parent.gameObject.SetActive(saleVisible);
            carTime.gameObject.SetActive(!saleVisible);
            if(saleVisible)
            {
                var sale=s.LastRecord.sale;
                transactionSale.text=$"매출 {Money(sale.RevenueWon)}  ·  원가 {Money(sale.CostWon)}  ·  이익 {Money(sale.ProfitWon)}\n"+
                    (sale.AtCostMilliliters>0?$"초과분 {sale.AtCostMilliliters/1000m:0.###} L는 원가 판매 · 추가 이익 0원":"주유한 실제 연료량으로 정산했습니다.");
            }
            bool overfillFeedback=s.State==SessionState.Feedback && s.LastRecord?.outcome==FuelingOutcome.Overfill;
            score.color=overfillFeedback?red:amber;
            RefreshCelebration(s);
            lossHistory.text=r.TotalPenalty>0?$"최근 감점 −{r.LastPenalty:N0}점  ·  누적 감점 −{r.TotalPenalty:N0}점":"";
            residualHint.text=s.Overfilled?"초과 실패로 즉시 차단되어\n연료가 더 들어가지 않습니다.":"손을 떼도 남은 압력으로\n주유가 계속됩니다.";
            pressureValue.text=s.Pressure.Value>0 && s.Pressure.Value<1?s.Pressure.Value.ToString("0.0"):s.Pressure.Value.ToString("0"); flow.text=$"{s.Flow:0.0} L/s";
            holdButton.interactable=s.CanAcceptInput;
            holdLabel.text=overfillFeedback?(s.OperatingEnded?"초과 주유 실패 · 영업 정산 중":"초과 주유 실패 · 다음 차량 준비 중"):s.State==SessionState.ForcedDecay?"시간 초과 · 감압 중…":
                s.State==SessionState.UnderfillWait?"다시 눌러 주유 이어가기":s.IsActiveCustomer && !s.Held && s.Pressure.Value>s.Vehicle.resistance?"감압 중 · 누르면 주유 재개":"누르고 있으면 압력 상승";
            if(s.OperatingEnded && s.CanAcceptInput) holdLabel.text=s.State==SessionState.UnderfillWait?"마지막 차량 · 다시 눌러 주유 계속":"마지막 차량 주유 · 길게 누르세요";
            holdBackground.color=overfillFeedback?red:s.Held?Hex("FFD58D"):amber;
            holdButton.transform.localScale=s.Held?new Vector3(.985f,.985f,1):Vector3.one;
            instruction.text=overfillFeedback?"허용 상한 초과 즉시 주유를 중단했습니다.":s.State==SessionState.ForcedDecay?"차량 저항까지 감압하면 바로 결과를 확정합니다.":s.Held?"손을 떼면 압력이 서서히 내려갑니다.":s.IsActiveCustomer && s.Pressure.Value>s.Vehicle.resistance?"흐름이 멈추면 판정 · 미달이면 다시 누르세요":"마우스 · 터치 · 스페이스 키를 길게 누르세요";
            if(s.OperatingEnded && s.CanAcceptInput) instruction.text="영업은 마감됐어요. 마지막 차량은 계속 주유할 수 있어요.";
            seedLabel.text=session.FixedSeedEnabled?$"고정 시드 {session.DebugSeed}":"시드 고정 OFF";
            debugStats.text=$"압력 {s.Pressure.Value:0.0}   ·   유량 {s.Flow:0.00} L/s\n기존 {request?.InitialFuel ?? 0} L  ·  목표 {request?.Target ?? 0} L\n제한 {request?.TimeLimit ?? 0:0.0} s  ·  허용 ±{request?.Tolerance ?? 0:0.0} L\n현재 {s.Fuel:0.00} L  ·  예측 최종 {s.PredictedFinal:0.00} L\n상승 {session.settings.pressureRise:0}/s · 감압 {session.settings.pressureDecay:0}/s\n계수 {session.settings.flowCoefficient:0.00}  ·  시드 {s.Seed}";
            if(v!=null)
            {
                carName.text=v.displayName; resistance.text=$"차량 저항 {v.resistance:0}";
                fuel.text=s.Overfilled?$">{request.MaximumFuel:0.00}":s.Fuel.ToString("0.00");
                fuelInfo.text=$"탱크 용량 {request.TankCapacity} L   ·   기존 연료 {request.InitialFuel} L";
                target.text=$"목표 {request.Target} L\n허용 {request.MinimumFuel:0.#}–{request.MaximumFuel:0.#} L";
                float maximum=(float)request.MaximumFuel+10;
                fuelFill.rectTransform.sizeDelta=new Vector2(Mathf.Clamp01((float)s.Fuel/maximum)*492,16);
                fuelFill.color=s.Fuel>request.MaximumFuel?red:s.Fuel>=request.MinimumFuel?mint:Hex("7AB7CB");
                targetBand.rectTransform.anchoredPosition=new Vector2(24+(float)request.MinimumFuel/maximum*492,-694);
                targetBand.rectTransform.sizeDelta=new Vector2((float)request.Tolerance*2/maximum*492,28);
                targetMarker.anchoredPosition=new Vector2(24+request.Target/maximum*492,-690);
                carTime.text=$"{(s.OperatingEnded?"마지막 차량 남은 시간":"남은 시간")}   {s.VehicleRemaining:0.0} / {request.TimeLimit:0.0} s";
                vehicleTimeFill.rectTransform.sizeDelta=new Vector2((float)(s.VehicleRemaining/request.TimeLimit)*492,3);
                vehicleTimeFill.color=s.VehicleRemaining<3?red:mint;
                gauge.pressure=(float)s.Pressure.Value; gauge.resistance=v.resistance; gauge.Refresh();
                station.carColor=v.color; station.suv=v.id=="suv"; station.flow=(float)s.Flow;
                if(s.State==SessionState.Arrival) station.carOffset=(float)(s.TransitionRemaining/Math.Max(.01,session.settings.arrivalSeconds))*285;
                else if(s.State==SessionState.Feedback) station.carOffset=(float)(1-s.TransitionRemaining/Math.Max(.01,s.FeedbackDuration))*90;
                else station.carOffset=0;
                station.Refresh();
                if(s.State==SessionState.Arrival) status.text="다음 차량이 들어옵니다…";
                else if(s.State==SessionState.ForcedDecay)
                {
                    string reason=s.TimedOut?"시간 초과":s.Overfilled?"과다 주유":"영업 마감";
                    status.text=$"{reason} · 감압 마무리 중 ({s.DecayRemaining:0.0} s)";
                }
                else if(s.State==SessionState.Feedback) status.text=overfillFeedback?"초과 주유 · 즉시 중단 · 실패 확정":"유량 0 · 최종 주유량 확정";
                else if(s.Held) status.text=s.Flow>0?"압력 상승 중 · 주유 중":"압력 상승 중 · 주유 준비";
                else if(s.IsActiveCustomer && s.Flow>0)
                    status.text=$"감압 중 · 잔류 압력으로 주유 중 ({s.DecayRemaining:0.0} s)";
                else status.text=s.Fuel>0?"주유가 부족해요 · 다시 눌러 이어가세요":"주유 준비 완료 · 버튼을 길게 누르세요";
                if(s.OperatingEnded && s.CanAcceptInput) status.text="영업 마감 · 마지막 차량 주유를 마무리하세요";
            }
            feedbackPanel.gameObject.SetActive(s.State==SessionState.Feedback);
            failurePanel.SetActive(overfillFeedback);
            if(overfillFeedback)
            {
                var failed=s.LastRecord;
                failurePenalty.text=$"−{-failed.score:N0}점";
                failureDetails.text=$"목표보다 +{failed.fuel-failed.target:0.00} L 주유\n허용 상한 {failed.target+failed.tolerance:0.##} L을 넘었습니다.";
            }
            if(s.State==SessionState.Feedback && s.LastRecord!=null)
            {
                var record=s.LastRecord;
                bool success=record.outcome==FuelingOutcome.Success;
                feedbackPanel.color=success?mint:record.outcome==FuelingOutcome.Overfill?red:amber;
                string text=success?(record.perfect?"PERFECT!":"SUCCESS!"):record.outcome==FuelingOutcome.Overfill?"과다 주유!":"시간 초과!";
                string quantity=record.outcome==FuelingOutcome.Overfill?$">{record.target+record.tolerance:0.00}":$"{record.fuel:0.00}";
                feedback.text=$"{text}   {record.score:+0;-0;0}   ·   {quantity} L";
            }
            if(s.State==SessionState.Result)
            {
                resultRevenue.text=Money(r.TotalRevenueWon);
                resultCost.text=Money(r.TotalCostWon);
                resultProfit.text=Money(r.TotalProfitWon);
                resultProfit.color=r.TotalProfitMilliWon<0?red:mint;
                resultScore.text=r.TotalScore.ToString("N0");
                resultScore.color=r.TotalScore<0?red:amber;
                resultLoss.text=r.TotalPenalty>0?$"이번 영업의 누적 감점 −{r.TotalPenalty:N0}점":"이번 영업의 감점 0점";
                if(s.LastRecord?.outcome==FuelingOutcome.Overfill)
                    resultLoss.text+=$"\n마지막 차량 주유 실패 · 초과 주유 · −{-s.LastRecord.score}점";
                string small=r.CountVolumeGroup(true)==0?"—":$"{r.AverageVolumeTime(true):0.00} s ({r.CountVolumeGroup(true)}대)";
                string large=r.CountVolumeGroup(false)==0?"—":$"{r.AverageVolumeTime(false):0.00} s ({r.CountVolumeGroup(false)}대)";
                volumeStats.text=$"소량 ≤35 L 평균 {small}\n대량 >35 L 평균 {large}";
                resultNote.text=$"마감 후 처리 {r.ClosingCount}대 · 실패 원인은 중복 집계";
                resultStats.text=$"성공 차량                          {r.SuccessCount} / {r.Records.Count}\n퍼펙트 / 원터치                         {r.PerfectCount} / {r.OneTouchCount}\n최대 연속 성공                         {r.MaximumStreak}\n평균 주유 오차                       {r.AverageError:0.00} L\n평균 처리 시간                       {r.AverageTime:0.00} s\n과다 주유 / 시간 초과                {r.OverfillCount} / {r.TimeoutCount}";
                resultNote.text+=$" · 보너스 +{r.TotalBonus:N0}점";
                resultExport.text=string.IsNullOrEmpty(session.ExportMessage)?$"최고 기록 {session.BestScore:N0}  ·  {session.BalanceName}"+((session.PracticeModified || session.BalanceName!="STANDARD")?" · 연습 영업":"")+$"\n마감 후 추가 시간 {s.ClosingElapsed:0.00} s":session.ExportMessage;
            }
        }

        static string Money(decimal won) => won.ToString("#,0.###")+"원";

        void CelebrateSuccess(FuelingRecord record)
        {
            celebratedRecord=record.outcome==FuelingOutcome.Success?record:null;
            if(celebratedRecord==null) return;
            celebrationStart=session.Simulation.Elapsed;
            // Use the existing departure time; never extend the wait for another car.
            celebrationDuration=Math.Max(.01,Math.Min(.85,session.Simulation.FeedbackDuration+(session.Simulation.OperatingEnded?0:session.settings.arrivalSeconds)));
            successTitle.text=record.OneTouch && record.perfect?"원터치 퍼펙트!":record.perfect?"퍼펙트!":record.OneTouch?"원터치 성공!":"주유 성공!";
            successPoints.text=$"+{record.score:N0}점";
            successBonuses.text=record.oneTouchBonus>0?$"원터치 +{record.oneTouchBonus}점":"";
            if(record.perfectBonus>0) successBonuses.text+=(successBonuses.text.Length>0?"  ·  ":"")+$"퍼펙트 +{record.perfectBonus}점";
            if(successBonuses.text.Length==0) successBonuses.text="좋아요! 허용 범위에 맞췄어요.";
            successTitle.color=record.OneTouch && record.perfect?amber:mint;
            successSparks.sparkleCount=record.OneTouch && record.perfect?14:record.perfect?12:record.OneTouch?9:6;
        }
        void RefreshCelebration(FuelingSimulation simulation)
        {
            double age=simulation.Elapsed-celebrationStart;
            bool visible=celebratedRecord!=null && ReferenceEquals(celebratedRecord,simulation.LastRecord)
                && celebratedRecord.transactionId==simulation.TransactionId && age>=0 && age<celebrationDuration
                && (simulation.State==SessionState.Feedback || simulation.State==SessionState.Arrival);
            successPanel.SetActive(visible);
            score.rectTransform.localScale=Vector3.one;
            if(!visible) return;
            float progress=(float)(age/celebrationDuration);
            float pulse=Mathf.Sin(Mathf.Clamp01(progress/.35f)*Mathf.PI);
            successOpacity.alpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.60f)/.40f));
            successPoints.rectTransform.anchoredPosition=new Vector2(94,-61+8*progress);
            successPoints.rectTransform.localScale=Vector3.one*(1+.05f*pulse);
            score.rectTransform.localScale=Vector3.one*(1+.045f*pulse);
            score.color=celebratedRecord.perfect?amber:mint;
            successSparks.effectProgress=progress; successSparks.Refresh();
        }

        RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); var rect=go.GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); return rect;
        }
        Image Box(Transform parent,string name,float x,float y,float w,float h,Color c)
        {
            var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>(); image.color=c; image.raycastTarget=false; return image;
        }
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color c,TextAnchor alignment=TextAnchor.MiddleLeft,bool bold=false)
        {
            var label=Rect(parent,"Text "+text,x,y,w,h).gameObject.AddComponent<Text>();
            label.text=text; label.font=font; label.fontSize=size; label.color=c; label.alignment=alignment;
            label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Overflow;
            label.raycastTarget=false; label.lineSpacing=1; return label;
        }
        Button Button(Transform parent,string text,float x,float y,float w,float h,Color background,Color foreground,Action action,int fontSize=18)
        {
            var image=Box(parent,text,x,y,w,h,background); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(1,1,1,.93f); colors.pressedColor=new Color(.82f,.82f,.82f,1); colors.disabledColor=new Color(.45f,.45f,.45f,1); button.colors=colors;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            Label(image.transform,text,6,0,w-12,h,fontSize,foreground,TextAnchor.MiddleCenter,true);
            if(action!=null) button.onClick.AddListener(()=>action()); return button;
        }
        PrototypeGraphic Art(Transform parent,float x,float y,float w,float h,PrototypeGraphic.Illustration kind)
        {
            var go=Rect(parent,kind.ToString(),x,y,w,h).gameObject;
            if(kind==PrototypeGraphic.Illustration.Station)
            {
                go.AddComponent<RectMask2D>();
                go=Rect(go.transform,"Station Artwork",0,0,w,h).gameObject;
            }
            go.AddComponent<CanvasRenderer>();
            var art=go.AddComponent<PrototypeGraphic>(); art.illustration=kind; art.raycastTarget=false; return art;
        }
        void OnDestroy()
        {
            if(session!=null && session.Simulation!=null) session.Simulation.Resolved-=CelebrateSuccess;
            if(canvas!=null) Destroy(canvas.gameObject);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace GasStation
{
    // Code-drawn artwork: no external sprites or material setup required.
    [RequireComponent(typeof(CanvasRenderer))]
    public class PrototypeGraphic : MaskableGraphic
    {
        public enum Illustration { Station, Gauge, Celebration }
        public Illustration illustration;
        public float pressure;
        public float resistance = 20;
        public Color carColor = new Color(1, .73f, .25f);
        public float carOffset;
        public bool suv;
        public float flow;
        public float effectProgress;
        public int sparkleCount = 8;
        public void Refresh() => SetVerticesDirty();
        VertexHelper mesh;
        Vector2 origin;
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh;
            origin = new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax);
            if (illustration == Illustration.Gauge) DrawGauge();
            else if (illustration == Illustration.Celebration) DrawCelebration();
            else DrawStation();
        }
        Vector2 Point(float x, float y) => origin + new Vector2(x, -y);
        void Polygon(Color c, params Vector2[] points)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) mesh.AddVert(Point(points[i].x, points[i].y), c, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }
        void Box(float x, float y, float w, float h, Color c) => Polygon(c, new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
        void Disc(float x, float y, float radius, Color c, int segments = 32)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Point(x,y),c,Vector2.zero);
            for (int i=0;i<=segments;i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                mesh.AddVert(Point(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),c,Vector2.zero);
                if(i>0) mesh.AddTriangle(first,first+i,first+i+1);
            }
        }
        void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized * width * .5f;
            Polygon(c,a+normal,b+normal,b-normal,a-normal);
        }
        void Arc(float x,float y,float radius,float start,float end,float width,Color c)
        {
            const int count=100;
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(start,end,(float)i/count)*Mathf.Deg2Rad;
                float b=Mathf.Lerp(start,end,(float)(i+1)/count)*Mathf.Deg2Rad;
                Line(new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),new Vector2(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius),width,c);
            }
        }
        void DrawGauge()
        {
            float x=rectTransform.rect.width*.5f,y=104;
            Disc(x,y,85,C("0E2827"));
            Arc(x,y,82,140,400,9,C("36534C"));
            if(pressure>.05f) Arc(x,y,82,140,140+260*pressure/100,9,C("FFBB55"));
            for(int i=0;i<=10;i++)
            {
                float a=(140+26*i)*Mathf.Deg2Rad;
                Line(new Vector2(x+Mathf.Cos(a)*65,y+Mathf.Sin(a)*65),new Vector2(x+Mathf.Cos(a)*72,y+Mathf.Sin(a)*72),i%5==0?3:1,C("7D9C8D"));
            }
            float threshold=(140+260*resistance/100)*Mathf.Deg2Rad;
            Line(new Vector2(x+Mathf.Cos(threshold)*76,y+Mathf.Sin(threshold)*76),new Vector2(x+Mathf.Cos(threshold)*92,y+Mathf.Sin(threshold)*92),3,C("75DCAA"));
            Disc(x+Mathf.Cos(threshold)*96,y+Mathf.Sin(threshold)*96,3,C("75DCAA"));
        }
        void DrawCelebration()
        {
            float progress = Mathf.Clamp01(effectProgress);
            for (int i = 0; i < sparkleCount; i++)
            {
                float angle = (i * 360f / sparkleCount + 13) * Mathf.Deg2Rad;
                float distance = 1 - Mathf.Pow(1 - progress, 3);
                float x = rectTransform.rect.width * .5f + Mathf.Cos(angle) * (185 + distance * 50);
                float y = 88 + Mathf.Sin(angle) * (73 + distance * 10);
                float size = (i % 3 == 0 ? 5 : 3) * (1 - progress * .5f);
                Color spark = i % 2 == 0 ? C("75DCAA") : C("FFBB55");
                Line(new Vector2(x-size,y), new Vector2(x+size,y), 2, spark);
                Line(new Vector2(x,y-size), new Vector2(x,y+size), 2, spark);
            }
        }
        void DrawStation()
        {
            Color ink=C("102A29"), pale=C("ECE9D9"), mint=C("75DCAA");
            // Distant buildings, canopy, and a single service lane.
            Box(0,0,492,280,C("203D37"));
            for(int i=0;i<9;i++)
            {
                float h=24+(i*17%30); Box(i*62,59-h,45,h,C("2A4940"));
                Box(i*62+12,65-h,7,9,C("3A5D4C"));
            }
            Box(0,204,492,76,C("19322F"));
            for(int i=0;i<8;i++) Box(i*72,247,34,3,C("476255"));
            Box(42,48,408,13,C("0B2422"));
            Box(42,33,408,17,C("E9B24F"));
            Box(54,50,10,155,C("335B4A")); Box(434,50,10,155,C("335B4A"));
            Box(67,58,27,3,pale); Box(399,58,27,3,pale);
            // Fuel pump.
            Box(77,114,59,91,ink); Box(73,110,59,89,pale); Box(79,119,47,30,ink);
            Box(87,128,30,3,mint); Box(87,137,21,3,mint);
            Box(82,157,17,17,C("E9B24F")); Box(109,156,10,4,C("AAB5A0"));
            Box(109,165,10,4,C("AAB5A0")); Box(69,194,67,9,C("9EAFA0"));
            Box(127,150,10,29,ink);
            // Hose from dispenser to rear fuel port.
            Vector2 a=new Vector2(135,157), b=new Vector2(163,259), c=new Vector2(177+carOffset,204), d=new Vector2(223+carOffset,168);
            Vector2 previous=a;
            for(int i=1;i<=30;i++)
            {
                float t=i/30f,u=1-t;
                Vector2 next=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;
                Line(previous,next,6,ink);
                if(flow>0) Line(previous,next,2,C("D9A853"));
                previous=next;
            }
            float ox=carOffset;
            // Side-view car, with a taller roof for the SUV.
            Box(201+ox,203,211,7,C("122724"));
            Disc(247+ox,195,20,ink); Disc(372+ox,195,20,ink);
            float roof=suv?103:120;
            Polygon(carColor,new Vector2(213+ox,157),new Vector2(242+ox,150),new Vector2(273+ox,roof),new Vector2(337+ox,roof),new Vector2(366+ox,150),new Vector2(402+ox,157),new Vector2(415+ox,177),new Vector2(409+ox,194),new Vector2(209+ox,194),new Vector2(204+ox,176));
            Color darkCar=Color.Lerp(carColor,ink,.18f);
            Box(210+ox,176,199,16,darkCar);
            Polygon(ink,new Vector2(253+ox,148),new Vector2(278+ox,roof+8),new Vector2(299+ox,roof+8),new Vector2(299+ox,148));
            Polygon(ink,new Vector2(307+ox,roof+8),new Vector2(332+ox,roof+8),new Vector2(355+ox,148),new Vector2(307+ox,148));
            Polygon(C("4D7B74"),new Vector2(257+ox,145),new Vector2(280+ox,roof+11),new Vector2(291+ox,roof+11),new Vector2(272+ox,145));
            Box(302+ox,153,2,36,darkCar); Box(279+ox,157,13,3,ink); Box(335+ox,157,13,3,ink);
            Box(405+ox,163,9,8,pale); Box(208+ox,163,7,8,C("EF7858"));
            Box(398+ox,187,19,6,pale); Box(202+ox,187,16,6,pale);
            Disc(247+ox,195,17,ink); Disc(372+ox,195,17,ink);
            Disc(247+ox,195,10,C("8EA69A")); Disc(372+ox,195,10,C("8EA69A"));
            Disc(247+ox,195,4,ink); Disc(372+ox,195,4,ink);
            Box(220+ox,164,10,7,ink); Line(d,d+new Vector2(7,9),7,ink);
        }
    }
}

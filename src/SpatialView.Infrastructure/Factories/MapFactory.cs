using SpatialView.Core.GisEngine;
using SpatialView.Core.Factories;
using SpatialView.Infrastructure.GisEngine;
using SpatialView.Engine.Geometry;

namespace SpatialView.Infrastructure.Factories;

/// <summary>
/// 맵 엔진 생성 팩토리
/// </summary>
public class MapFactory : IMapFactory
{
    public MapFactory()
    {
        // SpatialView 독립 엔진만 사용
        // SharpMap 의존성 완전 제거됨
    }
    
    /// <summary>
    /// SpatialView 맵 엔진 인스턴스 생성
    /// </summary>
    public IMapEngine CreateMapEngine()
    {
        // SpatialView 독립 엔진만 사용
        return new SpatialViewMapEngine();
    }
    
    /// <summary>
    /// SpatialView 맵 렌더러 인스턴스 생성
    /// </summary>
    public IMapRenderer CreateMapRenderer()
    {
        // SpatialView 독립 렌더러만 사용
        return new SpatialViewMapRenderer();
    }
    
    /// <summary>
    /// SpatialView 독립 엔진 기본 기능 테스트 (개발/디버깅용)
    /// </summary>
    public static bool TestFactoryAndEngine()
    {
        try
        {
            Console.WriteLine("🧪 SpatialView 독립 엔진 테스트 시작...");
            
            // 팩토리 생성
            var factory = new MapFactory();
            
            // SpatialView 엔진 생성 테스트
            var engine = factory.CreateMapEngine();
            if (engine == null)
            {
                Console.WriteLine("❌ SpatialView 엔진 생성 실패");
                return false;
            }
            
            if (!(engine is SpatialViewMapEngine))
            {
                Console.WriteLine("❌ 예상과 다른 엔진 타입이 생성됨 (SpatialViewMapEngine이어야 함)");
                return false;
            }
            
            // 기본 속성 테스트
            engine.Size = new System.Windows.Size(800, 600);
            engine.Center = new Coordinate(126.978, 37.5665);
            engine.Zoom = 12.0;
            engine.SRID = 4326;
            
            if (Math.Abs(engine.Size.Width - 800) > 0.1 || Math.Abs(engine.Size.Height - 600) > 0.1)
            {
                Console.WriteLine("❌ 엔진 크기 설정 실패");
                return false;
            }
            
            if (Math.Abs(engine.Center.X - 126.978) > 0.001 || Math.Abs(engine.Center.Y - 37.5665) > 0.001)
            {
                Console.WriteLine("❌ 엔진 중심점 설정 실패");
                return false;
            }
            
            // 좌표 변환 테스트
            var screenPoint = new System.Windows.Point(400, 300);
            var worldCoord = engine.ScreenToMap(screenPoint);
            var backToScreen = engine.MapToScreen(worldCoord);
            
            if (Math.Abs(backToScreen.X - 400) > 10 || Math.Abs(backToScreen.Y - 300) > 10)
            {
                Console.WriteLine("❌ 엔진 좌표 변환 실패");
                return false;
            }
            
            // 렌더러 생성 테스트
            var renderer = factory.CreateMapRenderer();
            if (renderer == null)
            {
                Console.WriteLine("❌ 맵 렌더러 생성 실패");
                return false;
            }
            
            if (!(renderer is SpatialViewMapRenderer))
            {
                Console.WriteLine("❌ 예상과 다른 렌더러 타입이 생성됨");
                return false;
            }
            
            Console.WriteLine("✅ SpatialView 독립 엔진 테스트 통과!");
            Console.WriteLine($"   - 엔진 타입: {engine.GetType().Name} (독립형)");
            Console.WriteLine($"   - 렌더러 타입: {renderer.GetType().Name} (독립형)");
            Console.WriteLine($"   - 엔진 크기: {engine.Size.Width}x{engine.Size.Height}");
            Console.WriteLine($"   - 중심좌표: ({engine.Center.X:F6}, {engine.Center.Y:F6})");
            Console.WriteLine($"   - 줌 레벨: {engine.Zoom}");
            Console.WriteLine($"   - 좌표계: EPSG:{engine.SRID}");
            Console.WriteLine("   🎉 SharpMap 의존성 완전 제거됨!");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 테스트 중 예외 발생: {ex.Message}");
            Console.WriteLine($"스택 트레이스: {ex.StackTrace}");
            return false;
        }
    }
}
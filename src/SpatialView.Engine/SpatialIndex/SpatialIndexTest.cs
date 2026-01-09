using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.SpatialIndex;

/// <summary>
/// 간단한 공간 인덱스 테스트 클래스
/// 컴파일 및 기본 동작 검증용
/// </summary>
public static class SpatialIndexTest
{
    /// <summary>
    /// R-Tree 기본 동작 테스트
    /// </summary>
    public static bool TestRTreeBasicOperations()
    {
        try
        {
            var rtree = new RTree<string>();
            
            // 테스트 데이터 추가
            rtree.Insert(new Envelope(0, 0, 10, 10), "Item1");
            rtree.Insert(new Envelope(5, 5, 15, 15), "Item2");
            rtree.Insert(new Envelope(20, 20, 30, 30), "Item3");
            
            // 기본 속성 확인
            if (rtree.Count != 3) return false;
            if (rtree.IsEmpty) return false;
            
            // 검색 테스트
            var results = rtree.Query(new Envelope(0, 0, 12, 12));
            if (results.Count != 2) return false; // Item1과 Item2가 검색되어야 함
            
            // 제거 테스트
            rtree.Remove(new Envelope(0, 0, 10, 10), "Item1");
            if (rtree.Count != 2) return false;
            
            // 전체 삭제 테스트
            rtree.Clear();
            if (!rtree.IsEmpty) return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Quad-Tree 기본 동작 테스트
    /// </summary>
    public static bool TestQuadtreeBasicOperations()
    {
        try
        {
            var bounds = new Envelope(0, 0, 100, 100);
            var quadtree = new Quadtree<string>(bounds);
            
            // 테스트 데이터 추가
            quadtree.Insert(new Envelope(10, 10, 10, 10), "Point1"); // 포인트로 처리
            quadtree.Insert(new Envelope(50, 50, 50, 50), "Point2");
            quadtree.Insert(new Envelope(80, 80, 80, 80), "Point3");
            
            // 기본 속성 확인
            if (quadtree.Count != 3) return false;
            if (quadtree.IsEmpty) return false;
            
            // 검색 테스트 - 좌상단 영역
            var results = quadtree.Query(new Envelope(0, 0, 60, 60));
            if (results.Count < 1) return false; // 최소 1개 이상 검색되어야 함
            
            // 제거 테스트
            quadtree.Remove(new Envelope(10, 10, 10, 10), "Point1");
            if (quadtree.Count != 2) return false;
            
            // 전체 삭제 테스트
            quadtree.Clear();
            if (!quadtree.IsEmpty) return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 모든 테스트 실행
    /// </summary>
    public static bool RunAllTests()
    {
        var rtreeTest = TestRTreeBasicOperations();
        var quadtreeTest = TestQuadtreeBasicOperations();
        
        return rtreeTest && quadtreeTest;
    }
}
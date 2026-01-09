using SpatialView.Engine.Geometry3D;

namespace SpatialView.Engine.Rendering3D;

/// <summary>
/// 3D 카메라 클래스
/// </summary>
public class Camera3D
{
    private Coordinate3D _position;
    private Coordinate3D _target;
    private Coordinate3D _up;
    private double _fieldOfView;
    private double _aspectRatio;
    private double _nearPlane;
    private double _farPlane;
    private bool _isDirty = true;
    private Matrix3D? _viewMatrix;
    private Matrix3D? _projectionMatrix;
    private Matrix3D? _viewProjectionMatrix;
    
    /// <summary>
    /// 카메라 위치
    /// </summary>
    public Coordinate3D Position
    {
        get => _position;
        set
        {
            _position = value;
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 카메라가 바라보는 대상 위치
    /// </summary>
    public Coordinate3D Target
    {
        get => _target;
        set
        {
            _target = value;
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 상향 벡터
    /// </summary>
    public Coordinate3D Up
    {
        get => _up;
        set
        {
            _up = value.Normalize();
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 시야각 (라디안)
    /// </summary>
    public double FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = Math.Clamp(value, 0.1, Math.PI - 0.1);
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 종횡비
    /// </summary>
    public double AspectRatio
    {
        get => _aspectRatio;
        set
        {
            _aspectRatio = value;
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 근평면 거리
    /// </summary>
    public double NearPlane
    {
        get => _nearPlane;
        set
        {
            _nearPlane = Math.Max(0.01, value);
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 원평면 거리
    /// </summary>
    public double FarPlane
    {
        get => _farPlane;
        set
        {
            _farPlane = Math.Max(_nearPlane + 0.01, value);
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// 뷰 행렬
    /// </summary>
    public Matrix3D ViewMatrix
    {
        get
        {
            if (_isDirty || _viewMatrix == null)
                UpdateMatrices();
            return _viewMatrix!;
        }
    }
    
    /// <summary>
    /// 프로젝션 행렬
    /// </summary>
    public Matrix3D ProjectionMatrix
    {
        get
        {
            if (_isDirty || _projectionMatrix == null)
                UpdateMatrices();
            return _projectionMatrix!;
        }
    }
    
    /// <summary>
    /// 뷰-프로젝션 결합 행렬
    /// </summary>
    public Matrix3D ViewProjectionMatrix
    {
        get
        {
            if (_isDirty || _viewProjectionMatrix == null)
                UpdateMatrices();
            return _viewProjectionMatrix!;
        }
    }
    
    public Camera3D()
    {
        _position = new Coordinate3D(0, 0, 10);
        _target = new Coordinate3D(0, 0, 0);
        _up = new Coordinate3D(0, 1, 0);
        _fieldOfView = Math.PI / 4; // 45도
        _aspectRatio = 1.0;
        _nearPlane = 0.1;
        _farPlane = 1000.0;
    }
    
    /// <summary>
    /// 카메라를 특정 바운딩 박스에 맞춰 조정
    /// </summary>
    public void FitToBox(Envelope3D box, double padding = 1.2)
    {
        if (box == null) return;
        
        // 박스 중심
        var centerX = (box.MinX + box.MaxX) / 2;
        var centerY = (box.MinY + box.MaxY) / 2;
        var centerZ = (box.MinZ + box.MaxZ) / 2;
        
        _target = new Coordinate3D(centerX, centerY, centerZ);
        
        // 박스 크기
        var size = Math.Max(Math.Max(box.Width, box.Height), box.Depth);
        
        // 카메라 거리 계산
        var distance = (size * padding) / (2 * Math.Tan(_fieldOfView / 2));
        
        // 카메라 위치 설정
        var direction = (_position - _target).Normalize();
        if (direction.X == 0 && direction.Y == 0 && direction.Z == 0)
            direction = new Coordinate3D(1, 1, 1).Normalize();
        
        _position = _target + direction * distance;
        _isDirty = true;
    }
    
    /// <summary>
    /// 궤도 회전
    /// </summary>
    public void Orbit(double azimuth, double elevation)
    {
        var direction = _position - _target;
        var distance = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        
        // 구면 좌표로 변환
        var (r, az, el) = CoordinateSystem3D.CartesianToSpherical(direction);
        
        // 각도 조정
        az += azimuth;
        el = Math.Clamp(el + elevation, -Math.PI / 2 + 0.01, Math.PI / 2 - 0.01);
        
        // 직교 좌표로 변환
        var newDirection = CoordinateSystem3D.SphericalToCartesian(distance, az, el);
        _position = _target + newDirection;
        
        _isDirty = true;
    }
    
    /// <summary>
    /// 카메라 이동 (팬)
    /// </summary>
    public void Pan(double dx, double dy)
    {
        var forward = (_target - _position).Normalize();
        var right = forward.CrossProduct(_up).Normalize();
        var up = right.CrossProduct(forward).Normalize();
        
        var offset = right * dx + up * dy;
        _position = _position + offset;
        _target = _target + offset;
        
        _isDirty = true;
    }
    
    /// <summary>
    /// 줌 (카메라를 타겟 방향으로 이동)
    /// </summary>
    public void Zoom(double factor)
    {
        var direction = _target - _position;
        var distance = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        
        // 최소/최대 거리 제한
        var newDistance = Math.Clamp(distance * factor, 0.1, 10000);
        var scaleFactor = newDistance / distance;
        
        direction = direction * scaleFactor;
        _position = _target - direction;
        
        _isDirty = true;
    }
    
    /// <summary>
    /// 월드 좌표를 스크린 좌표로 변환
    /// </summary>
    public (double x, double y, double z)? WorldToScreen(Coordinate3D worldPoint, int screenWidth, int screenHeight)
    {
        // 뷰-프로젝션 변환
        var viewProj = ViewProjectionMatrix;
        var transformed = viewProj.Transform(worldPoint);
        
        // 동차 좌표 나누기
        var w = transformed.Z; // 이 구현에서는 단순화
        if (Math.Abs(w) < double.Epsilon) return null;
        
        // NDC로 변환 (-1 ~ 1)
        var ndcX = transformed.X / w;
        var ndcY = transformed.Y / w;
        var ndcZ = transformed.Z / w;
        
        // 스크린 좌표로 변환
        var screenX = (ndcX + 1) * 0.5 * screenWidth;
        var screenY = (1 - ndcY) * 0.5 * screenHeight; // Y축 반전
        
        return (screenX, screenY, ndcZ);
    }
    
    /// <summary>
    /// 스크린 좌표를 레이로 변환
    /// </summary>
    public Ray3D ScreenToRay(double screenX, double screenY, int screenWidth, int screenHeight)
    {
        // NDC 좌표로 변환
        var ndcX = (screenX / screenWidth) * 2 - 1;
        var ndcY = 1 - (screenY / screenHeight) * 2; // Y축 반전
        
        // 역변환을 위한 포인트 생성
        var nearPoint = UnprojectPoint(ndcX, ndcY, -1);
        var farPoint = UnprojectPoint(ndcX, ndcY, 1);
        
        var direction = (farPoint - nearPoint).Normalize();
        return new Ray3D(nearPoint, direction);
    }
    
    private Coordinate3D UnprojectPoint(double ndcX, double ndcY, double ndcZ)
    {
        // 간단한 역투영 구현
        // 실제로는 역행렬을 사용해야 함
        var tanFov = Math.Tan(_fieldOfView * 0.5);
        var x = ndcX * _aspectRatio * tanFov;
        var y = ndcY * tanFov;
        var z = -1; // 카메라 방향
        
        var viewDir = new Coordinate3D(x, y, z).Normalize();
        
        // 카메라 공간에서 월드 공간으로 변환
        var forward = (_target - _position).Normalize();
        var right = forward.CrossProduct(_up).Normalize();
        var up = right.CrossProduct(forward).Normalize();
        
        var worldDir = right * viewDir.X + up * viewDir.Y - forward * viewDir.Z;
        
        return _position + worldDir * ((ndcZ + 1) * 0.5 * (_farPlane - _nearPlane) + _nearPlane);
    }
    
    private void UpdateMatrices()
    {
        // 뷰 행렬 생성
        _viewMatrix = CreateLookAtMatrix(_position, _target, _up);
        
        // 프로젝션 행렬 생성
        _projectionMatrix = CreatePerspectiveMatrix(_fieldOfView, _aspectRatio, _nearPlane, _farPlane);
        
        // 뷰-프로젝션 결합
        _viewProjectionMatrix = _projectionMatrix * _viewMatrix;
        
        _isDirty = false;
    }
    
    private static Matrix3D CreateLookAtMatrix(Coordinate3D eye, Coordinate3D target, Coordinate3D up)
    {
        var zAxis = (eye - target).Normalize();
        var xAxis = up.CrossProduct(zAxis).Normalize();
        var yAxis = zAxis.CrossProduct(xAxis);
        
        var matrix = new Matrix3D();
        
        // 회전 부분
        matrix.SetValue(0, 0, xAxis.X);
        matrix.SetValue(0, 1, xAxis.Y);
        matrix.SetValue(0, 2, xAxis.Z);
        
        matrix.SetValue(1, 0, yAxis.X);
        matrix.SetValue(1, 1, yAxis.Y);
        matrix.SetValue(1, 2, yAxis.Z);
        
        matrix.SetValue(2, 0, zAxis.X);
        matrix.SetValue(2, 1, zAxis.Y);
        matrix.SetValue(2, 2, zAxis.Z);
        
        // 이동 부분
        matrix.SetValue(0, 3, -xAxis.DotProduct(eye));
        matrix.SetValue(1, 3, -yAxis.DotProduct(eye));
        matrix.SetValue(2, 3, -zAxis.DotProduct(eye));
        
        return matrix;
    }
    
    private static Matrix3D CreatePerspectiveMatrix(double fov, double aspect, double near, double far)
    {
        var matrix = new Matrix3D();
        matrix.Clear(); // 모든 요소를 0으로
        
        var tanHalfFov = Math.Tan(fov / 2);
        
        matrix.SetValue(0, 0, 1 / (aspect * tanHalfFov));
        matrix.SetValue(1, 1, 1 / tanHalfFov);
        matrix.SetValue(2, 2, -(far + near) / (far - near));
        matrix.SetValue(2, 3, -(2 * far * near) / (far - near));
        matrix.SetValue(3, 2, -1);
        
        return matrix;
    }
}

/// <summary>
/// 3D 레이
/// </summary>
public class Ray3D
{
    public Coordinate3D Origin { get; }
    public Coordinate3D Direction { get; }
    
    public Ray3D(Coordinate3D origin, Coordinate3D direction)
    {
        Origin = origin;
        Direction = direction.Normalize();
    }
    
    /// <summary>
    /// 레이 상의 점 계산
    /// </summary>
    public Coordinate3D GetPoint(double distance)
    {
        return Origin + Direction * distance;
    }
    
    /// <summary>
    /// 평면과의 교차점 계산
    /// </summary>
    public Coordinate3D? IntersectPlane(Coordinate3D planePoint, Coordinate3D planeNormal)
    {
        var denominator = Direction.DotProduct(planeNormal);
        if (Math.Abs(denominator) < double.Epsilon)
            return null; // 레이와 평면이 평행
        
        var t = (planePoint - Origin).DotProduct(planeNormal) / denominator;
        if (t < 0)
            return null; // 교차점이 레이 뒤에 있음
        
        return GetPoint(t);
    }
}

/// <summary>
/// Matrix3D 확장 메서드
/// </summary>
public static class Matrix3DExtensions
{
    public static void SetValue(this Matrix3D matrix, int row, int col, double value)
    {
        // Matrix3D 클래스에 SetValue 메서드 추가 필요
        var field = matrix.GetType().GetField("_matrix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var array = (double[,])field.GetValue(matrix)!;
            array[row, col] = value;
        }
    }
    
    public static void Clear(this Matrix3D matrix)
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                matrix.SetValue(i, j, 0);
            }
        }
    }
}
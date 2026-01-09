using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Raster;

/// <summary>
/// 래스터 연산 구현
/// 재투영/리샘플링, 래스터 대수 연산, 지형 분석 등
/// </summary>
public static class RasterOperations
{
    /// <summary>
    /// 래스터 리샘플링
    /// </summary>
    /// <param name="source">원본 래스터</param>
    /// <param name="targetWidth">목표 너비</param>
    /// <param name="targetHeight">목표 높이</param>
    /// <param name="method">리샘플링 방법</param>
    /// <returns>리샘플링된 래스터</returns>
    public static RasterDataset Resample(RasterDataset source, int targetWidth, int targetHeight, 
        ResamplingMethod method = ResamplingMethod.Bilinear)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentException("Target dimensions must be positive");

        var result = new RasterDataset
        {
            Width = targetWidth,
            Height = targetHeight,
            SpatialReference = source.SpatialReference,
            NoDataValue = source.NoDataValue
        };

        // GeoTransform 조정
        result.GeoTransform = new GeoTransform
        {
            OriginX = source.GeoTransform.OriginX,
            OriginY = source.GeoTransform.OriginY,
            PixelWidth = source.GeoTransform.PixelWidth * source.Width / targetWidth,
            PixelHeight = source.GeoTransform.PixelHeight * source.Height / targetHeight,
            XSkew = source.GeoTransform.XSkew,
            YSkew = source.GeoTransform.YSkew
        };

        // 각 밴드에 대해 리샘플링 수행
        foreach (var sourceBand in source.Bands)
        {
            var targetBand = ResampleBand(sourceBand, targetWidth, targetHeight, method);
            result.Bands.Add(targetBand);
        }

        return result;
    }

    /// <summary>
    /// 래스터 재투영
    /// </summary>
    /// <param name="source">원본 래스터</param>
    /// <param name="targetSrs">목표 공간 참조 시스템</param>
    /// <param name="targetExtent">목표 범위 (선택사항)</param>
    /// <param name="targetResolution">목표 해상도 (선택사항)</param>
    /// <returns>재투영된 래스터</returns>
    public static RasterDataset Reproject(RasterDataset source, string targetSrs, 
        Envelope? targetExtent = null, double? targetResolution = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrEmpty(targetSrs)) throw new ArgumentException("Target SRS cannot be empty", nameof(targetSrs));

        // 간단한 구현: 동일한 투영계에서는 원본 반환
        if (source.SpatialReference == targetSrs)
        {
            return source;
        }

        var bounds = targetExtent ?? source.GetBounds();
        var resolution = targetResolution ?? Math.Min(
            source.GeoTransform.PixelWidth, 
            Math.Abs(source.GeoTransform.PixelHeight));

        var targetWidth = (int)Math.Ceiling(bounds.Width / resolution);
        var targetHeight = (int)Math.Ceiling(bounds.Height / resolution);

        var result = new RasterDataset
        {
            Width = targetWidth,
            Height = targetHeight,
            SpatialReference = targetSrs,
            NoDataValue = source.NoDataValue
        };

        result.GeoTransform = new GeoTransform
        {
            OriginX = bounds.MinX,
            OriginY = bounds.MaxY,
            PixelWidth = resolution,
            PixelHeight = -resolution,
            XSkew = 0,
            YSkew = 0
        };

        // 각 밴드를 재투영 (여기서는 단순 복사)
        foreach (var sourceBand in source.Bands)
        {
            var targetBand = new RasterBand(targetWidth, targetHeight, sourceBand.DataType, sourceBand.Name)
            {
                NoDataValue = sourceBand.NoDataValue
            };
            
            // 실제 재투영 로직은 복잡하므로 여기서는 기본 구조만 제공
            result.Bands.Add(targetBand);
        }

        return result;
    }

    /// <summary>
    /// 래스터 대수 연산 - 덧셈
    /// </summary>
    public static RasterDataset Add(RasterDataset raster1, RasterDataset raster2)
    {
        return ApplyBinaryOperation(raster1, raster2, (a, b) => a + b);
    }

    /// <summary>
    /// 래스터 대수 연산 - 뺄셈
    /// </summary>
    public static RasterDataset Subtract(RasterDataset raster1, RasterDataset raster2)
    {
        return ApplyBinaryOperation(raster1, raster2, (a, b) => a - b);
    }

    /// <summary>
    /// 래스터 대수 연산 - 곱셈
    /// </summary>
    public static RasterDataset Multiply(RasterDataset raster1, RasterDataset raster2)
    {
        return ApplyBinaryOperation(raster1, raster2, (a, b) => a * b);
    }

    /// <summary>
    /// 래스터 대수 연산 - 나눗셈
    /// </summary>
    public static RasterDataset Divide(RasterDataset raster1, RasterDataset raster2)
    {
        return ApplyBinaryOperation(raster1, raster2, (a, b) => b != 0 ? a / b : double.NaN);
    }

    /// <summary>
    /// 래스터와 상수 연산
    /// </summary>
    public static RasterDataset ApplyConstant(RasterDataset raster, double constant, MathOperation operation)
    {
        if (raster == null) throw new ArgumentNullException(nameof(raster));

        var result = new RasterDataset
        {
            Width = raster.Width,
            Height = raster.Height,
            GeoTransform = raster.GeoTransform,
            SpatialReference = raster.SpatialReference,
            NoDataValue = raster.NoDataValue
        };

        foreach (var sourceBand in raster.Bands)
        {
            var targetBand = new RasterBand(raster.Width, raster.Height, sourceBand.DataType, sourceBand.Name)
            {
                NoDataValue = sourceBand.NoDataValue
            };

            var sourceData = sourceBand.GetData();
            var targetData = new double[raster.Height, raster.Width];

            if (sourceData != null)
            {
                for (int y = 0; y < raster.Height; y++)
                {
                    for (int x = 0; x < raster.Width; x++)
                    {
                        var value = sourceData[y, x];
                        
                        if (sourceBand.NoDataValue.HasValue && 
                            Math.Abs(value - sourceBand.NoDataValue.Value) < 1e-10)
                        {
                            targetData[y, x] = value;
                            continue;
                        }

                        targetData[y, x] = operation switch
                        {
                            MathOperation.Add => value + constant,
                            MathOperation.Subtract => value - constant,
                            MathOperation.Multiply => value * constant,
                            MathOperation.Divide => constant != 0 ? value / constant : double.NaN,
                            MathOperation.Power => Math.Pow(value, constant),
                            _ => value
                        };
                    }
                }
            }

            targetBand.SetData(targetData);
            result.Bands.Add(targetBand);
        }

        return result;
    }

    /// <summary>
    /// 지형 분석 - 경사도 계산
    /// </summary>
    /// <param name="dem">수치 고도 모델</param>
    /// <param name="zFactor">Z 좌표 스케일 팩터</param>
    /// <param name="outputInDegrees">도 단위로 출력 여부</param>
    /// <returns>경사도 래스터</returns>
    public static RasterDataset CalculateSlope(RasterDataset dem, double zFactor = 1.0, bool outputInDegrees = true)
    {
        if (dem == null) throw new ArgumentNullException(nameof(dem));
        if (dem.BandCount == 0) throw new ArgumentException("DEM must have at least one band");

        var result = CreateResultRaster(dem, "Slope");
        var sourceBand = dem.Bands[0];
        var sourceData = sourceBand.GetData();
        var targetBand = result.Bands[0];
        var targetData = new double[dem.Height, dem.Width];

        if (sourceData != null)
        {
            var cellSizeX = Math.Abs(dem.GeoTransform.PixelWidth);
            var cellSizeY = Math.Abs(dem.GeoTransform.PixelHeight);

            for (int y = 1; y < dem.Height - 1; y++)
            {
                for (int x = 1; x < dem.Width - 1; x++)
                {
                    if (IsNoData(sourceBand, sourceData[y, x]))
                    {
                        targetData[y, x] = sourceBand.NoDataValue ?? double.NaN;
                        continue;
                    }

                    // 3x3 윈도우에서 경사 계산
                    var a = sourceData[y - 1, x - 1] * zFactor;
                    var b = sourceData[y - 1, x] * zFactor;
                    var c = sourceData[y - 1, x + 1] * zFactor;
                    var d = sourceData[y, x - 1] * zFactor;
                    var f = sourceData[y, x + 1] * zFactor;
                    var g = sourceData[y + 1, x - 1] * zFactor;
                    var h = sourceData[y + 1, x] * zFactor;
                    var i = sourceData[y + 1, x + 1] * zFactor;

                    var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / (8 * cellSizeX);
                    var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / (8 * cellSizeY);

                    var slopeRadians = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy));
                    targetData[y, x] = outputInDegrees ? slopeRadians * 180 / Math.PI : slopeRadians;
                }
            }
        }

        targetBand.SetData(targetData);
        return result;
    }

    /// <summary>
    /// 지형 분석 - 향(방향) 계산
    /// </summary>
    /// <param name="dem">수치 고도 모델</param>
    /// <param name="zFactor">Z 좌표 스케일 팩터</param>
    /// <returns>향 래스터 (도 단위)</returns>
    public static RasterDataset CalculateAspect(RasterDataset dem, double zFactor = 1.0)
    {
        if (dem == null) throw new ArgumentNullException(nameof(dem));
        if (dem.BandCount == 0) throw new ArgumentException("DEM must have at least one band");

        var result = CreateResultRaster(dem, "Aspect");
        var sourceBand = dem.Bands[0];
        var sourceData = sourceBand.GetData();
        var targetBand = result.Bands[0];
        var targetData = new double[dem.Height, dem.Width];

        if (sourceData != null)
        {
            var cellSizeX = Math.Abs(dem.GeoTransform.PixelWidth);
            var cellSizeY = Math.Abs(dem.GeoTransform.PixelHeight);

            for (int y = 1; y < dem.Height - 1; y++)
            {
                for (int x = 1; x < dem.Width - 1; x++)
                {
                    if (IsNoData(sourceBand, sourceData[y, x]))
                    {
                        targetData[y, x] = sourceBand.NoDataValue ?? double.NaN;
                        continue;
                    }

                    // 3x3 윈도우에서 향 계산
                    var a = sourceData[y - 1, x - 1] * zFactor;
                    var b = sourceData[y - 1, x] * zFactor;
                    var c = sourceData[y - 1, x + 1] * zFactor;
                    var d = sourceData[y, x - 1] * zFactor;
                    var f = sourceData[y, x + 1] * zFactor;
                    var g = sourceData[y + 1, x - 1] * zFactor;
                    var h = sourceData[y + 1, x] * zFactor;
                    var i = sourceData[y + 1, x + 1] * zFactor;

                    var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / (8 * cellSizeX);
                    var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / (8 * cellSizeY);

                    var aspectRadians = Math.Atan2(dzdy, -dzdx);
                    var aspectDegrees = aspectRadians * 180 / Math.PI;
                    
                    // 0-360도 범위로 정규화
                    if (aspectDegrees < 0) aspectDegrees += 360;

                    targetData[y, x] = aspectDegrees;
                }
            }
        }

        targetBand.SetData(targetData);
        return result;
    }

    /// <summary>
    /// 지형 분석 - 음영 기복도 계산
    /// </summary>
    /// <param name="dem">수치 고도 모델</param>
    /// <param name="azimuth">태양 방위각 (도)</param>
    /// <param name="altitude">태양 고도각 (도)</param>
    /// <param name="zFactor">Z 좌표 스케일 팩터</param>
    /// <returns>음영 기복도 래스터</returns>
    public static RasterDataset CalculateHillshade(RasterDataset dem, double azimuth = 315, double altitude = 45, double zFactor = 1.0)
    {
        if (dem == null) throw new ArgumentNullException(nameof(dem));
        if (dem.BandCount == 0) throw new ArgumentException("DEM must have at least one band");

        var result = CreateResultRaster(dem, "Hillshade");
        var sourceBand = dem.Bands[0];
        var sourceData = sourceBand.GetData();
        var targetBand = result.Bands[0];
        var targetData = new double[dem.Height, dem.Width];

        if (sourceData != null)
        {
            var cellSizeX = Math.Abs(dem.GeoTransform.PixelWidth);
            var cellSizeY = Math.Abs(dem.GeoTransform.PixelHeight);

            // 태양각을 라디안으로 변환
            var azimuthRad = azimuth * Math.PI / 180;
            var altitudeRad = altitude * Math.PI / 180;

            for (int y = 1; y < dem.Height - 1; y++)
            {
                for (int x = 1; x < dem.Width - 1; x++)
                {
                    if (IsNoData(sourceBand, sourceData[y, x]))
                    {
                        targetData[y, x] = sourceBand.NoDataValue ?? double.NaN;
                        continue;
                    }

                    // 3x3 윈도우에서 음영 계산
                    var a = sourceData[y - 1, x - 1] * zFactor;
                    var b = sourceData[y - 1, x] * zFactor;
                    var c = sourceData[y - 1, x + 1] * zFactor;
                    var d = sourceData[y, x - 1] * zFactor;
                    var f = sourceData[y, x + 1] * zFactor;
                    var g = sourceData[y + 1, x - 1] * zFactor;
                    var h = sourceData[y + 1, x] * zFactor;
                    var i = sourceData[y + 1, x + 1] * zFactor;

                    var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / (8 * cellSizeX);
                    var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / (8 * cellSizeY);

                    var slopeRad = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy));
                    var aspectRad = Math.Atan2(dzdy, -dzdx);

                    var hillshade = Math.Sin(altitudeRad) * Math.Sin(Math.PI / 2 - slopeRad) +
                                    Math.Cos(altitudeRad) * Math.Cos(Math.PI / 2 - slopeRad) *
                                    Math.Cos(azimuthRad - aspectRad);

                    // 0-255 범위로 정규화
                    targetData[y, x] = Math.Max(0, Math.Min(255, hillshade * 255));
                }
            }
        }

        targetBand.SetData(targetData);
        return result;
    }

    /// <summary>
    /// 래스터 필터링 - 커널 기반
    /// </summary>
    /// <param name="source">원본 래스터</param>
    /// <param name="kernel">필터 커널</param>
    /// <returns>필터링된 래스터</returns>
    public static RasterDataset ApplyFilter(RasterDataset source, double[,] kernel)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));

        var result = CreateResultRaster(source, "Filtered");
        var kernelHeight = kernel.GetLength(0);
        var kernelWidth = kernel.GetLength(1);
        var offsetY = kernelHeight / 2;
        var offsetX = kernelWidth / 2;

        for (int bandIndex = 0; bandIndex < source.BandCount; bandIndex++)
        {
            var sourceBand = source.Bands[bandIndex];
            var targetBand = result.Bands[bandIndex];
            var sourceData = sourceBand.GetData();
            var targetData = new double[source.Height, source.Width];

            if (sourceData != null)
            {
                for (int y = offsetY; y < source.Height - offsetY; y++)
                {
                    for (int x = offsetX; x < source.Width - offsetX; x++)
                    {
                        if (IsNoData(sourceBand, sourceData[y, x]))
                        {
                            targetData[y, x] = sourceBand.NoDataValue ?? double.NaN;
                            continue;
                        }

                        double sum = 0;
                        double weightSum = 0;

                        for (int ky = 0; ky < kernelHeight; ky++)
                        {
                            for (int kx = 0; kx < kernelWidth; kx++)
                            {
                                var sourceY = y + ky - offsetY;
                                var sourceX = x + kx - offsetX;
                                var value = sourceData[sourceY, sourceX];
                                var weight = kernel[ky, kx];

                                if (!IsNoData(sourceBand, value))
                                {
                                    sum += value * weight;
                                    weightSum += weight;
                                }
                            }
                        }

                        targetData[y, x] = weightSum != 0 ? sum / weightSum : sourceBand.NoDataValue ?? double.NaN;
                    }
                }
            }

            targetBand.SetData(targetData);
        }

        return result;
    }

    /// <summary>
    /// 미리 정의된 필터들
    /// </summary>
    public static class Filters
    {
        /// <summary>
        /// 가우시안 블러 필터
        /// </summary>
        public static double[,] GaussianBlur(int size = 3, double sigma = 1.0)
        {
            var kernel = new double[size, size];
            var offset = size / 2;
            var sum = 0.0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var dx = x - offset;
                    var dy = y - offset;
                    var value = Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                    kernel[y, x] = value;
                    sum += value;
                }
            }

            // 정규화
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] /= sum;
                }
            }

            return kernel;
        }

        /// <summary>
        /// 엣지 검출 필터 (Sobel)
        /// </summary>
        public static double[,] SobelX()
        {
            return new double[,]
            {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };
        }

        public static double[,] SobelY()
        {
            return new double[,]
            {
                { -1, -2, -1 },
                {  0,  0,  0 },
                {  1,  2,  1 }
            };
        }

        /// <summary>
        /// 평균 필터
        /// </summary>
        public static double[,] Mean(int size = 3)
        {
            var kernel = new double[size, size];
            var value = 1.0 / (size * size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] = value;
                }
            }

            return kernel;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// 밴드 리샘플링
    /// </summary>
    private static RasterBand ResampleBand(RasterBand source, int targetWidth, int targetHeight, ResamplingMethod method)
    {
        var target = new RasterBand(targetWidth, targetHeight, source.DataType, source.Name)
        {
            NoDataValue = source.NoDataValue
        };

        var sourceData = source.GetData();
        var targetData = new double[targetHeight, targetWidth];

        if (sourceData != null)
        {
            var xRatio = (double)source.Width / targetWidth;
            var yRatio = (double)source.Height / targetHeight;

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    var sourceX = x * xRatio;
                    var sourceY = y * yRatio;

                    targetData[y, x] = method switch
                    {
                        ResamplingMethod.NearestNeighbor => SampleNearestNeighbor(sourceData, sourceX, sourceY, source),
                        ResamplingMethod.Bilinear => SampleBilinear(sourceData, sourceX, sourceY, source),
                        ResamplingMethod.Cubic => SampleCubic(sourceData, sourceX, sourceY, source),
                        _ => SampleNearestNeighbor(sourceData, sourceX, sourceY, source)
                    };
                }
            }
        }

        target.SetData(targetData);
        return target;
    }

    /// <summary>
    /// 최근접 이웃 샘플링
    /// </summary>
    private static double SampleNearestNeighbor(double[,] data, double x, double y, RasterBand band)
    {
        var ix = (int)Math.Round(x);
        var iy = (int)Math.Round(y);

        if (ix < 0 || ix >= data.GetLength(1) || iy < 0 || iy >= data.GetLength(0))
            return band.NoDataValue ?? double.NaN;

        return data[iy, ix];
    }

    /// <summary>
    /// 이중선형 보간 샘플링
    /// </summary>
    private static double SampleBilinear(double[,] data, double x, double y, RasterBand band)
    {
        var x1 = (int)Math.Floor(x);
        var y1 = (int)Math.Floor(y);
        var x2 = x1 + 1;
        var y2 = y1 + 1;

        if (x1 < 0 || x2 >= data.GetLength(1) || y1 < 0 || y2 >= data.GetLength(0))
            return SampleNearestNeighbor(data, x, y, band);

        var fx = x - x1;
        var fy = y - y1;

        var v11 = data[y1, x1];
        var v12 = data[y2, x1];
        var v21 = data[y1, x2];
        var v22 = data[y2, x2];

        // NoData 값 처리
        if (IsNoData(band, v11) || IsNoData(band, v12) || IsNoData(band, v21) || IsNoData(band, v22))
            return band.NoDataValue ?? double.NaN;

        var i1 = v11 * (1 - fx) + v21 * fx;
        var i2 = v12 * (1 - fx) + v22 * fx;

        return i1 * (1 - fy) + i2 * fy;
    }

    /// <summary>
    /// 3차 보간 샘플링 (간단한 구현)
    /// </summary>
    private static double SampleCubic(double[,] data, double x, double y, RasterBand band)
    {
        // 간단한 구현을 위해 이중선형 보간 사용
        return SampleBilinear(data, x, y, band);
    }

    /// <summary>
    /// 이진 연산 적용
    /// </summary>
    private static RasterDataset ApplyBinaryOperation(RasterDataset raster1, RasterDataset raster2, Func<double, double, double> operation)
    {
        if (raster1 == null) throw new ArgumentNullException(nameof(raster1));
        if (raster2 == null) throw new ArgumentNullException(nameof(raster2));
        if (raster1.Width != raster2.Width || raster1.Height != raster2.Height)
            throw new ArgumentException("Rasters must have the same dimensions");

        var result = new RasterDataset
        {
            Width = raster1.Width,
            Height = raster1.Height,
            GeoTransform = raster1.GeoTransform,
            SpatialReference = raster1.SpatialReference,
            NoDataValue = raster1.NoDataValue
        };

        var bandCount = Math.Min(raster1.BandCount, raster2.BandCount);
        
        for (int i = 0; i < bandCount; i++)
        {
            var band1 = raster1.Bands[i];
            var band2 = raster2.Bands[i];
            var targetBand = new RasterBand(raster1.Width, raster1.Height, band1.DataType, band1.Name)
            {
                NoDataValue = band1.NoDataValue
            };

            var data1 = band1.GetData();
            var data2 = band2.GetData();
            var targetData = new double[raster1.Height, raster1.Width];

            if (data1 != null && data2 != null)
            {
                for (int y = 0; y < raster1.Height; y++)
                {
                    for (int x = 0; x < raster1.Width; x++)
                    {
                        var value1 = data1[y, x];
                        var value2 = data2[y, x];

                        if (IsNoData(band1, value1) || IsNoData(band2, value2))
                        {
                            targetData[y, x] = band1.NoDataValue ?? double.NaN;
                        }
                        else
                        {
                            targetData[y, x] = operation(value1, value2);
                        }
                    }
                }
            }

            targetBand.SetData(targetData);
            result.Bands.Add(targetBand);
        }

        return result;
    }

    /// <summary>
    /// 결과 래스터 생성
    /// </summary>
    private static RasterDataset CreateResultRaster(RasterDataset source, string nameSuffix)
    {
        var result = new RasterDataset
        {
            Width = source.Width,
            Height = source.Height,
            GeoTransform = source.GeoTransform,
            SpatialReference = source.SpatialReference,
            NoDataValue = source.NoDataValue
        };

        foreach (var sourceBand in source.Bands)
        {
            var targetBand = new RasterBand(source.Width, source.Height, sourceBand.DataType, $"{sourceBand.Name}_{nameSuffix}")
            {
                NoDataValue = sourceBand.NoDataValue
            };
            result.Bands.Add(targetBand);
        }

        return result;
    }

    /// <summary>
    /// NoData 값 확인
    /// </summary>
    private static bool IsNoData(RasterBand band, double value)
    {
        return band.NoDataValue.HasValue && Math.Abs(value - band.NoDataValue.Value) < 1e-10;
    }

    #endregion
}

/// <summary>
/// 수학 연산 타입
/// </summary>
public enum MathOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power
}
﻿using System.Diagnostics;
using System.Text.RegularExpressions;

using OpenCvSharp;

using Range = OpenCvSharp.Range;

#region Methods
void PrintImage(string name, Mat image)
{
    try
    {
        // Show image
        _ = new Window(name, WindowFlags.GuiExpanded);
        Cv2.ImShow(name, image);
    }
    catch (Exception)
    {
        Console.WriteLine("Error while printing image");
    }
}

void PrintMat(string name, Mat mat)
{
    Console.WriteLine(StringifyMat(name, mat));
}

string StringifyMat(string name, Mat mat)
{
    string text = "";
    try
    {
        text += $"{name}:\n";
        for (int i = 0; i < mat.Rows; i++)
        {
            string txt = "";
            for (int j = 0; j < mat.Cols; j++)
            {
                var pixel = mat.At<byte>(i, j);
                txt += pixel + " ";
            }
            text += $"[{txt}]\n";
        }
    }
    catch (Exception)
    {
        Console.WriteLine("Error while printing mat");
    }

    return text + '\n';
}

bool IsMax(Mat mat, Point point, int neighbors, out string log)
{
    bool isMax = false;
    byte pixel = mat.At<byte>(point.X, point.Y);
    int n = (int)Math.Sqrt(neighbors);

    // Try to find fit n neighbors on x axis around point
    int left = point.Y - n / 2;
    int right = point.Y + n / 2;
    if (left < 0)
    {
        right += -left;
        left = 0;
    }
    if (right >= mat.Cols)
    {
        left -= right - mat.Cols;
        right = mat.Cols;
    }

    // Try to find fit n neighbors on y axis around point
    int top = point.X - n / 2;
    int bottom = point.X + n / 2;
    if (top < 0)
    {
        bottom += -top;
        top = 0;
    }
    if (bottom >= mat.Rows)
    {
        top -= bottom - mat.Rows;
        bottom = mat.Rows;
    }

    // Create submat with fit neighbors
    Mat submat = mat.SubMat(new Range(top, bottom), new Range(left, right));

    log = $"Checking if {pixel} is max of his {neighbors} neighbors\n";
    log += StringifyMat("Submat", submat);

    for (int row = top; row < bottom; row++)
        for (int column = left; column < right; column++)
        {
            if (row == point.X && column == point.Y)
                continue;
            if (row < 0 || column < 0)
                continue;
            if (row >= mat.Rows || column >= mat.Cols)
                continue;

            byte neighborPixel = mat.At<byte>(row, column);

            if (neighborPixel < pixel)
            {
                isMax = true;
                break;
            }
        }

    log += $"Result is that {pixel} is {(isMax ? "" : "not")} max of his {neighbors} neighbors\n";

    return isMax;
}

Mat OptimizeCircleDetectionByGradient(Mat image, int threshold, double gradientDirectionThreshold)
{
    Mat blurredImage = new();
    Size size = new Size(image.Rows / 10, image.Cols / 10);
    if (size.Width % 2 == 0 || size.Width <= 0)
        size.Width++;
    if (size.Height % 2 == 0 || size.Height <= 0)
        size.Height++;
    Cv2.GaussianBlur(image, blurredImage, size, 1.4, 1.4);

    Mat blurredImage_gray = new();
    Cv2.CvtColor(blurredImage, blurredImage_gray, ColorConversionCodes.BGR2GRAY);

    Mat gradientX = new();
    Mat gradientY = new();
    Cv2.Sobel(blurredImage_gray, gradientX, MatType.CV_64FC1, 1, 0, 1);
    Cv2.Sobel(blurredImage_gray, gradientY, MatType.CV_64FC1, 0, 1, 1);

    // Perform gradient direction check and accumulate only for circles in the gradient direction.
    Mat sobelImage = new Mat();
    Cv2.AddWeighted(gradientX, 0.5, gradientY, 0.5, 0, sobelImage);

    Mat thresholdImage = new Mat();
    Cv2.Threshold(sobelImage, thresholdImage, threshold, 255, ThresholdTypes.Binary);

    return thresholdImage; // Modify and return the result based on gradient direction.
}

Mat OptimizeCircleDetectionUsingPyramids(Mat image, int initialRadius, int scaleFactor, int numberOfScales)
{
    List<Mat> pyramidImages = new()
    {
        image.Clone()
    };

    // Create an image pyramid by reducing the image size multiple times.
    for (int i = 1; i <= numberOfScales; i++)
    {
        Mat scaledImage = new();
        Cv2.PyrDown(pyramidImages[i - 1], scaledImage);
        pyramidImages.Add(scaledImage);
    }

    Mat circleImage = new Mat(); // Initialize the result image.

    for (int scale = numberOfScales; scale >= 0; scale--)
    {
        int radius = initialRadius * (int)Math.Pow(scaleFactor, scale);

        if (scale < numberOfScales)
        {
            // Refine the circle positions at each scale and update the 'circleImage' accordingly.
            Mat currentImage = pyramidImages[scale];
            Mat currentCircleImage = DetectCirclesAtScale(currentImage, radius);

            // Upsample and merge currentCircleImage with the higher resolution circleImage.
            if (scale < numberOfScales)
            {
                Mat upscaledCircleImage = new Mat();
                Cv2.PyrUp(currentCircleImage, upscaledCircleImage);
                Cv2.AddWeighted(circleImage, 1.0, upscaledCircleImage, 1.0, 0, circleImage);
            }
            else
            {
                circleImage = currentCircleImage.Clone();
            }
        }
        else
        {
            // Perform circle detection at the original image resolution.
            circleImage = DetectCirclesAtScale(image, radius);
        }
    }

    return circleImage;
}

Mat DetectCirclesAtScale(Mat image, int radius)
{
    Mat grayImage = new Mat();
    Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);

    Mat blurredImage = new Mat();
    Cv2.GaussianBlur(grayImage, blurredImage, new Size(9, 9), 2, 2);

    Mat edges = new Mat();
    Cv2.Canny(blurredImage, edges, 100, 200);

    Mat circleImage = new Mat(image.Size(), MatType.CV_8UC3, Scalar.All(0));

    var contours = Cv2.FindContoursAsArray(edges, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

    foreach (var contour in contours)
    {
        var area = Cv2.ContourArea(contour);
        if (area > 1000) // You may adjust this threshold based on your image size and requirements.
        {
            var perimeter = Cv2.ArcLength(contour, true);
            var circularity = 4 * Math.PI * area / (perimeter * perimeter);

            if (circularity > 0.6) // You may adjust this circularity threshold.
            {
                Point2f center;
                Cv2.MinEnclosingCircle(contour, out center, out _);
                var centerPoint = new Point((int)center.X, (int)center.Y);

                // Draw a circle on the output image.
                Cv2.Circle(circleImage, centerPoint, radius, new Scalar(0, 0, 255), 2);
            }
        }
    }

    return circleImage;
}



Mat CircleDetection(Mat image, out Mat acc, out Mat circleImage, out int count)
{
    // Gaussian filter
    Mat blurredImage = new();
    Size size = new Size(image.Rows / 10, image.Cols / 10);
    if (size.Width % 2 == 0 || size.Width <= 0)
        size.Width++;
    if (size.Height % 2 == 0 || size.Height <= 0)
        size.Height++;
    Cv2.GaussianBlur(image, blurredImage, size, 1.4, 1.4);

    PrintImage("blurredImage", blurredImage);

    // Sobel filter
    Mat sobelImage = new();
    Mat blurredImage_gray = new();

    Cv2.CvtColor(blurredImage, blurredImage_gray, ColorConversionCodes.BGR2GRAY);

    Mat gradientX = new();
    Mat gradientY = new();

    //Cv2.Scharr(blurredImage_gray, grad, MatType.CV_16SC1, 1, 1);
    Cv2.Sobel(blurredImage_gray, gradientX, MatType.CV_64FC1, 1, 0, 1);
    Cv2.Sobel(blurredImage_gray, gradientY, MatType.CV_64FC1, 0, 1, 1);

    Cv2.ConvertScaleAbs(gradientX, gradientX);
    Cv2.ConvertScaleAbs(gradientY, gradientY);

    Cv2.AddWeighted(gradientX, 0.5, gradientY, 0.5, 0, sobelImage);

    PrintImage("sobelImage", sobelImage);

    // Threshold
    Mat thresholdImage = new();
    Cv2.Threshold(sobelImage, thresholdImage, 15, 255, ThresholdTypes.Binary);

    PrintImage("thresholdImage", thresholdImage);

    // Create accumulator
    acc = new(image.Rows, image.Cols, MatType.CV_32SC1, 0);

    // Calculate acc
    var maxDistance = Math.Sqrt(Math.Pow(thresholdImage.Rows, 2) + Math.Pow(thresholdImage.Cols, 2));

    for (int row1 = 0; row1 < thresholdImage.Rows; row1++)
        for (int column1 = 0; column1 < thresholdImage.Cols; column1++)
        {
            var pixel1 = thresholdImage.At<byte>(row1, column1);

            if (pixel1 != 255)
                continue;

            for (int row2 = 0; row2 < thresholdImage.Rows; row2++)
                for (int column2 = 0; column2 < thresholdImage.Cols; column2++)
                {
                    var pixel2 = thresholdImage.At<byte>(row2, column2);

                    if (pixel2 != 255)
                        continue;

                    var distance = Math.Sqrt(Math.Pow(row1 - row2, 2) + Math.Pow(column1 - column2, 2));

                    if (distance < maxDistance)
                    {
                        acc.At<int>(row2, column2) += (int)distance;
                    }
                }

        }

    // Print accumulator
    PrintMat("acc", acc);

    // Normalize the accumulator
    Cv2.Normalize(acc, acc, 0, 255, NormTypes.MinMax);

    // Print normalized accumulator
    PrintMat("acc", acc);

    // Find max in accumulator
    circleImage = Mat.Zeros(acc.Rows, acc.Cols, MatType.CV_8UC3);
    //image.CopyTo(circleImage);

    count = 0;
    for (int rowAcc = 0; rowAcc < acc.Rows; rowAcc++)
        for (int columnAcc = 0; columnAcc < acc.Cols; columnAcc++)
        {
            // If pixel is max in 26 neighbors draw circle
            if (IsMax(acc, new Point(rowAcc, columnAcc), 26, out string maxLog))
            {
                Console.WriteLine(maxLog);

                Console.WriteLine($"Max at ({rowAcc}, {columnAcc}) = {acc.At<byte>(rowAcc, columnAcc)}\n\n");
                // Choose the color based on the value of the pixel by choosing in a green to red
                Vec3b color = new()
                {
                    Item0 = (byte)(255 - acc.At<byte>(rowAcc, columnAcc)),
                    Item1 = acc.At<byte>(rowAcc, columnAcc),
                    Item2 = 0
                };

                circleImage.Set(rowAcc, columnAcc, color /*new Vec3b(253, 50, 197)*/);
                count++;
            }
        }

    return circleImage;
}

#endregion Methods

// List of images to test on
List<string> images = new() {
    @"Resources\images\coins.png", // 0
    @"Resources\images\coins2.jpg", // 1
    @"Resources\images\MoonCoin.png", // 2
    @"Resources\images\four.png", // 3
    @"Resources\images\fourn.png" // 4
};

// Choose image to test on
string imagePath = images[1];

// Load image
Mat image = Cv2.ImRead(imagePath);

PrintImage("image", image);



var watch1 = Stopwatch.StartNew();
Mat acc, circleImage;
int count;
Mat circleImage1 = new();
try
{
    circleImage1 = CircleDetection(image.Clone(), out acc, out circleImage, out count);
    Console.WriteLine($"Count = {count} on total of {acc.Rows * acc.Cols}");
}
catch (Exception _)
{
}
watch1.Stop();

// Get start time
var watch2 = Stopwatch.StartNew();
Mat circleImage2 = new();
try
{
    circleImage2 = OptimizeCircleDetectionByGradient(image.Clone(), 15, 30);
}
catch (Exception _)
{
}
watch2.Stop();

// Get start time
var watch3 = Stopwatch.StartNew();
Mat circleImage3 = new();
try
{
    circleImage3 = OptimizeCircleDetectionUsingPyramids(image.Clone(), 10, 2, 3);
}
catch (Exception _)
{
}
watch3.Stop();

// Print results
Console.WriteLine($"Time for CircleDetection: {watch1.ElapsedMilliseconds} ms");
Console.WriteLine($"Time for OptimizeCircleDetectionByGradient: {watch2.ElapsedMilliseconds} ms");
Console.WriteLine($"Time for OptimizeCircleDetectionUsingPyramids: {watch3.ElapsedMilliseconds} ms");

// Print images
PrintImage("circleImage1", circleImage1);
PrintImage("circleImage2", circleImage2);
PrintImage("circleImage3", circleImage3);

Cv2.WaitKey(0);

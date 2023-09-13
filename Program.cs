﻿using System.Diagnostics;
using System.Text.RegularExpressions;

using OpenCvSharp;

using Range = OpenCvSharp.Range;

/*
On rappelle le fonctionnement de l’algorithme de detection de cercles :
1. (optionnel) Filtrage Gaussien (en cas de bruit ou de details tres fins)
2. Filtrage de Sobel, calcul de la magnitude de gradient Imag dans chaque pixel
3. Tous les pixels dont la magnitude est au dessus d’une fraction t de la valeur maximale dans Imag
sont consideres comme des pixels du contour. Note : visualisez l’image des contours pour etre surs
que vous avez dedans les contours des objets recherches.
4. Initialisez toutes les valeurs de l’accumulateur acc a 0.
5. Pour chaque pixel de contour, considerez toutes les (r,c) possibles, calculez le rayon rad pour que
le cercle situe en (r,c) passe par le pixel respectif, et incrementez dans l’accumulateur la case qui
correspond a (r, c, rad).
6. Identifiez dans l’accumulateur les maximas locaux - les cases avec des valeurs superieures aux 26
cases voisines (car l’accumulateur est tridimensionnel).
7. Selectionnez les N valeurs les plus grandes, et a partir des indices (i,j,k) recuperez les (r,c,rad)
correspondants et visualisez les cercles en passant par OpenCV.
Note 1 : les cercles plus grands recoivent plus de votes, donc il faudrait normaliser les valeurs de
l’accumulateur pour ne pas privilegier les cercles grands.
Note 2 : pour mettre un vote, on peut incrementer soit par 1, soit par la magnitude du gradient dans
le pixel respectif etc.
Essayez de trouver une solution qui fonctionne pour des images variees (voir par exemple Figure 1
pour des images avec ou sans bruit).
 */

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
#endregion Methods


string imagePath = @"Resources\images\four.png";

// Load image
Mat image = Cv2.ImRead(imagePath);

PrintImage("image", image);

// Gaussian filter
Mat blurredImage = new();
Cv2.GaussianBlur(image, blurredImage, new Size(5, 5), 0.4, 0.4);

PrintImage("blurredImage", blurredImage);

// Sobel filter
Mat sobelImage = new();
Cv2.Sobel(blurredImage, sobelImage, MatType.CV_8UC1, 1, 1);

PrintImage("sobelImage", sobelImage);

// Threshold
Mat thresholdImage = new();
Cv2.Threshold(sobelImage, thresholdImage, 20, 255, ThresholdTypes.Binary);

PrintImage("thresholdImage", thresholdImage);

// Create accumulator
Mat acc = new(image.Rows, image.Cols, MatType.CV_32SC1, 0);

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
Mat circleImage = Mat.Zeros(acc.Rows, acc.Cols, MatType.CV_8UC3);
//image.CopyTo(circleImage);

int count = 0;
for (int rowAcc = 0; rowAcc < acc.Rows; rowAcc++)
    for (int columnAcc = 0; columnAcc < acc.Cols; columnAcc++)
    {
        string maxLog;
        // If pixel is max in 26 neighbors draw circle
        if (IsMax(acc, new Point(rowAcc, columnAcc), 26, out maxLog))
        {
            Console.WriteLine(maxLog);

            Console.WriteLine($"Max at ({rowAcc}, {columnAcc}) = {acc.At<byte>(rowAcc, columnAcc)}\n\n");
            circleImage.Set(rowAcc, columnAcc, new Vec3b(253, 50, 197));
            count++;
        }
    }

Console.WriteLine($"Count = {count} on total of {acc.Rows * acc.Cols}");

PrintImage("circleImage", circleImage);
PrintMat("circleImage", circleImage);

Cv2.WaitKey(0);

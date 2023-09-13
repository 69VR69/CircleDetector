using System.Diagnostics;
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
Note 2 : pour mettre un vote, on peut incr´ementer soit par 1, soit par la magnitude du gradient dans
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
    try
    {
        Console.WriteLine($"{name} = ");
        for (int i = 0; i < mat.Rows; i++)
        {
            string txt = "";
            for (int j = 0; j < mat.Cols; j++)
            {
                var pixel = mat.At<byte>(i, j);

                if (pixel > 0)
                {
                    txt += pixel + " ";
                }
            }
            Console.WriteLine($"[{txt}]");
        }
    }
    catch (Exception)
    {
        Console.WriteLine("Error while printing mat");
    }
}

bool IsMax(Mat mat, Point point, int neighbors)
{
    bool isMax = true;

    int neighborLeft = point.X - neighbors / 2;
    int neighborRight = point.X + neighbors / 2;
    int neighborTop = point.Y - neighbors / 2;
    int neighborBottom = point.Y + neighbors / 2;

    byte pixel = mat.At<byte>(point.Y, point.X);

    for (int row = neighborTop; row < neighborBottom; row++)
        for (int column = neighborLeft; column < neighborRight; column++)
        {
            if (row == point.Y && column == point.X)
                continue;
            if (row < 0 || column < 0)
                continue;
            if (row >= mat.Rows || column >= mat.Cols)
                continue;

            byte neighborPixel = mat.At<byte>(row, column);

            if (neighborPixel > pixel)
            {
                isMax = false;
                break;
            }
        }

    return isMax;
}
#endregion Methods


string imagePath = @"Resources\images\four.png";

// Load image
Mat image = Cv2.ImRead(imagePath);

PrintImage("image", image);

// Gaussian filter
Mat blurredImage = new Mat();
Cv2.GaussianBlur(image, blurredImage, new Size(1, 1), 0);

PrintImage("blurredImage", blurredImage);

// Sobel filter
Mat sobelImage = new Mat();
Cv2.Sobel(blurredImage, sobelImage, MatType.CV_8UC1, 1, 1);

PrintImage("sobelImage", sobelImage);

// Threshold
Mat thresholdImage = new Mat();
Cv2.Threshold(sobelImage, thresholdImage, 80, 255, ThresholdTypes.Binary);

PrintImage("thresholdImage", thresholdImage);

// Create accumulator
Mat acc = new(image.Rows, image.Cols, MatType.CV_32SC1, 0);

// Calculate radius
var maxDistance = Math.Sqrt(Math.Pow(thresholdImage.Rows, 2) + Math.Pow(thresholdImage.Cols, 2)) / 2;

for (int row1 = 0; row1 < thresholdImage.Rows; row1++)
    for (int column1 = 0; column1 < thresholdImage.Cols; column1++)
    {
        var pixel = thresholdImage.At<byte>(row1, column1);

        if (pixel != 255)
            continue;

        for (int row2 = 0; row2 < thresholdImage.Rows; row2++)
            for (int column2 = 0; column2 < thresholdImage.Cols; column2++)
            {
                var distance = Math.Sqrt(Math.Pow(row1 - row2, 2) + Math.Pow(column1 - column2, 2));

                if (distance < maxDistance)
                {
                    acc.At<int>(row2, column2) += 1;
                }
            }

    }

// Print accumulator
PrintMat("acc", acc);

// Find max in 26 neighbors and draw circle on circleImage
Mat circleImage = new();
image.CopyTo(circleImage);

int count = 0;
for (int rowAcc = 0; rowAcc < acc.Rows; rowAcc++)
    for (int columnAcc = 0; columnAcc < acc.Cols; columnAcc++)
    {
        // If pixel is max in 26 neighbors draw circle
        if (IsMax(acc, new Point(columnAcc, rowAcc), 26))
        {
            Console.WriteLine($"Max at ({rowAcc}, {columnAcc}) = {acc.At<int>(rowAcc, columnAcc)}");
            Cv2.Line(circleImage, new Point(columnAcc, rowAcc), new Point(columnAcc, rowAcc), Scalar.Red, 1);
            count++;
        }
    }

Console.WriteLine($"Count = {count} on total of {acc.Rows * acc.Cols}");

PrintImage("circleImage", circleImage);

Cv2.WaitKey(0);

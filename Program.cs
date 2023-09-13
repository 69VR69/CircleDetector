using System.Diagnostics;

using OpenCvSharp;

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
Cv2.Threshold(sobelImage, thresholdImage, 100, 255, ThresholdTypes.Binary);

PrintImage("thresholdImage", thresholdImage);

// Create accumulator
int width = thresholdImage.Width;
int height = thresholdImage.Height;
int radius = 100;
int[,,] accumulator = new int[width, height, radius];
for (int i = 0; i < width; i++)
{
    for (int j = 0; j < height; j++)
    {
        for (int k = 0; k < radius; k++)
        {
            accumulator[i, j, k] = 0;
        }
    }
}

// Find circles
for (int i = 0; i < width; i++)
{
    for (int j = 0; j < height; j++)
    {
        if (thresholdImage.At<byte>(i, j) == 255)
        {
            for (int k = 0; k < radius; k++)
            {
                int a = i + k;
                int b = j + k;
                if (a < width && b < height)
                {
                    accumulator[a, b, k]++;
                }
            }
        }
    }
}

// Find max
int max = 0;
int maxI = 0;
int maxJ = 0;
int maxK = 0;
for (int i = 0; i < width; i++)
{
    for (int j = 0; j < height; j++)
    {
        for (int k = 0; k < radius; k++)
        {
            if (accumulator[i, j, k] > max)
            {
                max = accumulator[i, j, k];
                maxI = i;
                maxJ = j;
                maxK = k;
            }
        }
    }
}

// Draw circle
Cv2.Circle(image, new Point(maxI, maxJ), maxK, Scalar.Red, 2);

PrintImage("image", image);


Cv2.WaitKey(0);

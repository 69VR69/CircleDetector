using OpenCvSharp;

/*
On rappelle le fonctionnement de l’algorithme de detection de cercles :
1. (optionnel) Filtrage Gaussien (en cas de bruit ou de details tres fins)
2. Filtrage de Sobel, calcul de la magnitude de gradient Imag dans chaque pixel
3. Tous les pixels dont la magnitude est au dessus d’une fraction t de la valeur maximale dans Imag
sont consideres comme des pixels du contour. Note : visualisez l’image des contours pour etre surs
que vous avez dedans les contours des objets recherches.
4. Initialisez toutes les valeurs de l’accumulateur acc a 0.
5. Pour chaque pixel de contour, consid´erez toutes les (r,c) possibles, calculez le rayon rad pour que
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

// Load image
Mat image = new Mat();

// Gaussian filter

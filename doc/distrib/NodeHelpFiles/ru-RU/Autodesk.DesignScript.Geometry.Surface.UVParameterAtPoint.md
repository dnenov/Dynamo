## Подробности
UVParameterAtPoint позволяет найти положение UV поверхности во входной точке на поверхности. Если входная точка не находится на поверхности, этот узел находит на поверхности точку, ближайшую ко входной. В примере ниже сначала создается поверхность с помощью узла BySweep2Rails. Затем в Code Block задается точка, в которой требуется найти параметр UV. Точка не находится на поверхности, поэтому узел использует в качестве положения для поиска параметра UV ближайшую точку на поверхности.
___
## Файл примера

![UVParameterAtPoint](./Autodesk.DesignScript.Geometry.Surface.UVParameterAtPoint_img.jpg)

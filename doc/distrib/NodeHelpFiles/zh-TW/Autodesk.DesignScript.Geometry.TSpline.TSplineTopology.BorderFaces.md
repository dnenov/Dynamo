## 深入資訊
`TSplineTopology.BorderFaces` 會傳回 T 雲形線曲面中包含的邊界面清單。

以下範例透過 `TSplineSurface.ByCylinderPointsRadius` 建立兩個 T 雲形線曲面: 一個是開放曲面，另一個是使用 `TSplineSurface.Thicken` 增厚的曲面，這會讓該曲面變成封閉曲面。使用 `TSplineTopology.BorderFaces` 節點檢驗兩個曲面時，第一個會傳回邊界面的清單，第二個會傳回空清單，因為曲面是封閉的，所以沒有邊界面。
___
## 範例檔案

![TSplineTopology.BorderFaces](./Autodesk.DesignScript.Geometry.TSpline.TSplineTopology.BorderFaces_img.jpg)
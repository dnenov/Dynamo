## Подробности
`List.NormalizeDepth` возвращает новый список равномерной глубины до заданного места или глубины списка.

Как и узлы `List.Flatten`, узлы `List.NormalizeDepth` можно использовать для возврата одномерного списка (списка с одним уровнем). Однако его также можно использовать для добавления уровней списка. Узел нормализует входной список по глубине, выбранной пользователем.

В примере ниже список, содержащий два списка с неравной глубиной, можно нормализовать по различным уровням с помощью регулятора целых чисел. При нормализации глубины по разным уровням список увеличивается или уменьшается по глубине, но всегда остается равномерным. Список с уровнем 1 возвращает один список элементов, а список с уровнем 3 возвращает два уровня вложенных списков.
___
## Файл примера

![List.NormalizeDepth](./DSCore.List.NormalizeDepth_img.jpg)
## 상세
조건부 제어 노드로 작동하는 경우 'test' 입력은 부울 값을 가져오지만 'true' 및 'false' 입력은 모든 데이터 유형을 허용할 수 있습니다. 테스트 값이 'true'인 경우 이 노드는 'true' 입력에서 항목을 반환하고, 테스트가 'false' 입력인 경우 노드는 'false' 입력에서 항목을 반환합니다. 아래 예에서는 먼저 0~99 범위에서 임의 숫자 리스트를 생성합니다. 리스트의 항목 수는 정수 슬라이더로 제어합니다. 두 번째 숫자 슬라이더에 의해 결정된 두 번째 숫자로 나누어 떨어지는지를 테스트하기 위해 코드 블록과 공식 'x%a==0'을 사용합니다. 이렇게 하면 임의 리스트의 항목이 두 번째 정수 슬라이더에 의해 결정된 수로 나누어 떨어지는지 여부에 해당하는 부울 값 리스트가 생성됩니다. 이 부울 값 리스트는 If 노드에 대한 'test' 입력으로 사용됩니다. 기본 구를 'true' 입력으로 사용하고 기본 직육면체를 'false' 입력으로 사용합니다. If 노드의 결과는 구 또는 직육면체의 리스트입니다. 마지막으로 Translate 노드를 사용하여 형상 리스트를 분산합니다.

IF는 모든 노드 AS THOUGH SET TO SHORTEST를 복제합니다. 첨부된 예제에서 그 이유를 확인할 수 있습니다. 특히, LONGEST가 공식 노드에 적용되어 있고 조건부의 "짧은" 분기가 통과될 때 결과를 살펴보면 이유를 확인할 수 있습니다. 이러한 변경은 단일 부울 입력 또는 부울 리스트를 사용할 때 예측 가능한 동작을 허용하도록 수행되었습니다.
___
## 예제 파일

![If](./CoreNodeModels.Logic.RefactoredIf_img.jpg)

using UnityEngine;

/// <summary>
/// AttributeType 관련 메타(설명 등)를 중앙에서 관리합니다.
/// 필요시 텍스트를 수정하거나 확장하세요.
/// </summary>
public static class AttributeInfo
{
    public static string GetDescription(AttributeType type)
    {
        switch (type)
        {
            case AttributeType.WoodSord:
                return "[목검]\n\n라인 완성 시,\n<color=Green>8</color>의 추가 피해를 입힙니다.\n\n피해 유형: 단일";
            case AttributeType.WoodShield:
                return "[목방패]\n\n라인 완성 시,\n피해를 막아 주는\n방어막을 <color=Green>10</color> 생성합니다.\n\n지속 턴: 4턴, 갱신 가능";
            case AttributeType.Staff:
                return "[지팡이]\n\n라인 완성 시,\n<color=Green>모든 적</color>에게\n<color=Green>10</color>의 추가 피해를 입힙니다.\n\n피해 유형: 광역";
            case AttributeType.BratCandy:
                return "[개초딩 사탕]\n\n<color=Red>장착된 슬롯과 동일 방향(가로,세로)인 모든 라인이</color>\n완성시<color=Green>+10</color>의 추가 피해를 입힙니다.\n\n피해 유형: 단일";
            case AttributeType.Cross:
                return "[십자가]\n\n줄 완성 시,\n적 HP를 <color=Green>10</color> 흡수합니다.\n\n라운드 종료 시에도 회복이 유지됩니다.";
            default:
                return string.Empty;
        }
    }
}
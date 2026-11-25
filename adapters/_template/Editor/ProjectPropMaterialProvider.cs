using PropRecipe;
using UnityEngine;

namespace PropRecipe.Adapters
{
    /// <summary>
    /// 어댑터 템플릿: 이 프로젝트의 재질 규칙을 여기에 넣는다. 프로젝트 쪽(예: Assets/&lt;Project&gt;/PropKitAdapter/Editor/)으로
    /// 복사한 뒤 네임스페이스·클래스 이름을 바꿔 채운다. 채우기 전에는 키트 기본 공급자(파이프라인 기본 셰이더)를 그대로 쓴다.
    /// 확인할 것: 새 셰이더를 만들어도 되는 프로젝트인지, 기존 재질을 복사해야 하는지, 팔레트 텍스처를 받는 속성이 있는지.
    /// </summary>
    public sealed class ProjectPropMaterialProvider : IPropMaterialProvider
    {
        private readonly PropDefaultMaterialProvider _fallback;

        public ProjectPropMaterialProvider(string materialFolder)
        {
            _fallback = new PropDefaultMaterialProvider(materialFolder);
        }

        public Material GetMaterial(string key, Color color, float glow)
        {
            // TODO: 프로젝트 재질 규칙(예: 원본 재질 복사 후 색만 변경)
            return _fallback.GetMaterial(key, color, glow);
        }

        public Material GetPaletteMaterial(string propId, Texture2D palette)
        {
            // TODO: 팔레트 텍스처를 받는 속성 이름, 그림자·하이라이트 처리
            return _fallback.GetPaletteMaterial(propId, palette);
        }
    }
}

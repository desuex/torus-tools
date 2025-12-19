namespace TorusTool.Models;

public enum HunkSectionType : int
{
    VertexShader = 0,
    ShaderProgram = 1,
    TSEDataTable = 2,
    RenderSprite = 3,
    TSEStringTable = 4,
    TSEFontDescriptor = 5,
    EntityTemplate = 6,
    ClankBodyTemplate = 8,
    LiteScript = 10,
    SqueakSample = 11,
    TSETexture = 13,
    RenderModelTemplate = 14,
    RenderModelTemplate2 = 15, // Duplicate in BT?
    SqueakStream = 17,
    StateFlowTemplate = 18
}

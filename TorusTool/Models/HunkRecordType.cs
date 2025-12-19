namespace TorusTool.Models;

public enum HunkRecordType : int
{
    Header = 0x40070,
    FilenameHeader = 0x40071,
    Empty = 0x40072,
    AbstractHashIdentifier = 0x40002,
    TSEStringTableMain = 0x4100F,

    ClankBodyTemplateMain = 0x45100,
    ClankBodyTemplateSecondary = 0x402100,
    ClankBodyTemplateName = 0x43100,
    ClankBodyTemplateData = 0x44100,
    ClankBodyTemplateData2 = 0x404100,

    LiteScriptMain = 0x4300c,
    LiteScriptData = 0x4200c,
    LiteScriptData2 = 0x4100c,

    SqueakSampleData = 0x204090,

    TSETextureHeader = 0x41150,
    TSETextureData = 0x40151,
    TSETextureData2 = 0x801151,
    TSETextureDataPS3 = 0x800151,
    TSETextureDataWii = 0x202151,

    RenderModelTemplateHeader = 0x101050,
    RenderModelTemplateData = 0x40054,
    RenderModelTemplateDataTable = 0x20055,

    AnimationData = 0x42005,
    AnimationData2 = 0x41005,

    RenderSpriteData = 0x41007,
    TSERenderSprite = 0x41007,
    EffectsParamsData = 0x43112,

    TSEFontDescriptorData = 0x43087,

    TSEDataTableData1 = 0x43083,
    TSEDataTableData2 = 0x4008a,

    StateFlowTemplateData = 0x43088,
    StateFlowTemplateData2 = 0x42088,

    SqueakStreamData = 0x204092,
    SqueakStreamData2 = 0x201092,

    EntityPlacementData = 0x42009,
    EntityPlacementData2 = 0x103009,
    EntityPlacementBCCData = 0x101009,
    EntityPlacementLevelData = 0x102009,

    EntityTemplateData = 0x101008,
}

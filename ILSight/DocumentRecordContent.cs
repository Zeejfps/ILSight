using ILSight.DiagramArea;

namespace ILSight;

/// <summary>What an open tab showed, as the settings keep it: a node, a diagram or a comparison.</summary>
public union DocumentRecordContent(NodeDocumentRecord, DiagramDocumentRecord, ComparisonDocumentRecord);

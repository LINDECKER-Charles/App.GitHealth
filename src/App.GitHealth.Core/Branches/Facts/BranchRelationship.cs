namespace App.GitHealth.Core.Branches;

public enum BranchRelationship
{
    SameCommit,
    CommonAncestor,
    BranchIsAncestorOfReference,
    NoCommonAncestor,
}

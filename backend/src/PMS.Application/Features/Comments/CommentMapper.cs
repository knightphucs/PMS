using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Comments;

[Mapper]
public partial class CommentMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty(nameof(Comment.EmployeeId), nameof(CommentResponse.AuthorId))]
    [MapProperty("Author.Name", nameof(CommentResponse.AuthorName))]
    public partial CommentResponse ToResponse(Comment comment);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}

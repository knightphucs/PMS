using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Attachments;

[Mapper]
public partial class AttachmentMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty("Uploader.Name", nameof(AttachmentResponse.UploaderName))]
    public partial AttachmentResponse ToResponse(Attachment attachment);
#pragma warning restore RMG020
}

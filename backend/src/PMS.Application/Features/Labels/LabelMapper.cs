using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Labels;

[Mapper]
public partial class LabelMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial LabelResponse ToResponse(Label label);
#pragma warning restore RMG020
}

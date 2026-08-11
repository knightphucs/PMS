namespace PMS.Application.Features.CustomFields;

public interface ICustomFieldService
{
    Task<IReadOnlyList<FieldDefinitionResponse>> ListAsync(
        Guid projectId, CancellationToken ct = default);

    Task<FieldDefinitionResponse> CreateAsync(
        Guid projectId, CreateFieldDefinitionRequest request, CancellationToken ct = default);

    Task<FieldDefinitionResponse> UpdateAsync(
        Guid fieldId, UpdateFieldDefinitionRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid fieldId, CancellationToken ct = default);

    Task ReorderAsync(
        Guid projectId, ReorderFieldDefinitionsRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<FieldValueResponse>> GetValuesAsync(
        Guid taskId, CancellationToken ct = default);

    Task<IReadOnlyList<FieldValueResponse>> SetValuesAsync(
        Guid taskId, SetFieldValuesRequest request, CancellationToken ct = default);
}

using System;
using System.Collections.Generic;
using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Features.Admissions.GetAllAdmissions;

public sealed record GetAllAdmissionsQuery() : IQuery<Result<GetAllAdmissionsResponse>>;

public sealed record GetAllAdmissionsResponse(
    List<AdmissionSummaryResponse> Admissions);

public sealed record AdmissionSummaryResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string AcademicYear,
    DateTime AdmissionDate,
    int CourseCount);
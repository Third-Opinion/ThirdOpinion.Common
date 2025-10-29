# Migrate code from FhirTools /Users/ken/code/ThirdOpinion/FhirTools/FhirTools.sln to ThirdOpinion.Common and add to the NuGet package
-Do not add code related to running migrations from CSV files.
- Create a new project under aws ThirdOpinion.Common.Aws.HealthLake that support access AWS healthlake. It should copy the code from /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Fhir into two project.One for athena ThirdOpinion.Common.AthenaEhr and for HealthLake
  named ThirdOpinion.Common.Aws.HealthLake. /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/OAuth should be added to the AthenaEhr project.
- It should the code from /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Aws into another Project named  ThirdOpinion.Common.Aws.Misc
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Documents should be added to a new project ThirdOpinion.Common.Fhir.Documents. Do not include DocumentCsvReaderService
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/SecretsManager should be added to ThirdOpinion.Common.Aws.Misc
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Retry and /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/RateLimiting should be added to ThirdOpinion.Common.Misc
- Add /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Logging to ThirdOpinion.Common.Logging
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Bedrock to a new Project ThirdOpinion.Common.Aws.Bedrock
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools/Langfuse to a new Project ThirdOpinion.Common.Langfuse
- All projects should be added to the NuGet package
- Copy the unit tests for each from /Users/ken/code/ThirdOpinion/FhirTools/FhirTools.Tests
- /Users/ken/code/ThirdOpinion/FhirTools/FhirTools.FunctionalTests should be copied and updated to ThirdOpinion.Common/ThirdOpinion.Common.FunctionalTests
- Create a consolidated MD file so claude code knows how to use each project. Keep it high level.
# ThirdOpinion.Common.Fhir Documentation

This document provides an overview of the comprehensive FHIR builder documentation structure for the
ThirdOpinion.Common.Fhir library.

## Documentation Structure

The library documentation has been organized into modular README files located within each builder's directory for
improved maintainability and developer experience.

### Main Documentation

- **[ThirdOpinion.Common.Fhir/README.md](./ThirdOpinion.Common.Fhir/README.md)** - Main project overview, architecture,
  and quick start guide

### Builder-Specific Documentation

- **[Base/README.md](./ThirdOpinion.Common.Fhir/Builders/Base/README.md)** - Core infrastructure classes and
  `AiResourceBuilderBase<T>`
- **[Conditions/README.md](./ThirdOpinion.Common.Fhir/Builders/Conditions/README.md)** - HSDM Assessment Condition
  Builder documentation
- **[Observations/README.md](./ThirdOpinion.Common.Fhir/Builders/Observations/README.md)** - ADT Status, PSA
  Progression, and RECIST Progression builders
- **[Documents/README.md](./ThirdOpinion.Common.Fhir/Builders/Documents/README.md)** - OCR and Fact Extraction document
  reference builders
- **[Devices/README.md](./ThirdOpinion.Common.Fhir/Builders/Devices/README.md)** - AI Device registration and management
- **[Provenance/README.md](./ThirdOpinion.Common.Fhir/Builders/Provenance/README.md)** - Audit trails and regulatory
  compliance

## Key Features

Each documentation file includes:

- **Comprehensive API reference** with required and optional methods
- **Complete usage examples** with real-world scenarios
- **JSON output examples** showing generated FHIR resources
- **AWS integration patterns** for cloud deployment
- **Validation and error handling** guidance
- **Best practices** for clinical integration
- **Regulatory compliance** examples (FDA, EU MDR)

## Quick Navigation

### For Developers New to FHIR

Start with the [main README](./ThirdOpinion.Common.Fhir/README.md) for project overview and quick start examples.

### For Implementation

Review the [Base infrastructure](./ThirdOpinion.Common.Fhir/Builders/Base/README.md) to understand the common patterns,
then dive into specific builder documentation.

### For Clinical Integration

Each builder's README includes clinical workflow examples and integration patterns with existing EHR systems.

### For Regulatory Compliance

The [Provenance documentation](./ThirdOpinion.Common.Fhir/Builders/Provenance/README.md)
and [Devices documentation](./ThirdOpinion.Common.Fhir/Builders/Devices/README.md) provide comprehensive compliance
examples.

## Archive

The original monolithic PRD document has been archived as `.archive/prd2-original.md` for reference. All current
information is maintained in the modular documentation structure described above.

## Contributing

When adding new builders or modifying existing ones:

1. Update the appropriate builder-specific README
2. Include comprehensive examples and API documentation
3. Add integration patterns and best practices
4. Update this navigation document if adding new builder categories

For questions about the documentation structure or content, refer to the individual README files or the project
maintainers.
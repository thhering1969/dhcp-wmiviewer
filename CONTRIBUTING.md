# Contributing to DHCP WMI Viewer

Thank you for your interest in contributing to DHCP WMI Viewer! 🎉

## 🤝 How to Contribute

### Reporting Bugs 🐛
- Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.md)
- Include detailed environment information
- Provide steps to reproduce the issue
- Attach relevant log files if available

### Suggesting Features 💡
- Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.md)
- Describe the use case clearly
- Explain why this feature would be valuable

### Code Contributions 👨‍💻

#### Getting Started
1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes
4. Test thoroughly
5. Commit with clear messages
6. Push to your fork
7. Create a Pull Request

#### Development Setup
```bash
# Clone your fork
git clone https://github.com/YOUR-USERNAME/kurzzeit-dhcp-wmiviewer.git
cd kurzzeit-dhcp-wmiviewer

# Restore dependencies
dotnet restore

# Build and test
dotnet build
dotnet run
```

#### Code Standards
- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Include error handling with specific exceptions
- Use async/await for I/O operations
- Write unit tests for new features

#### Commit Messages
Use conventional commit format:
```
type(scope): description

feat(dhcp): add new reservation management feature
fix(ui): resolve DataGridView refresh issue
docs(readme): update installation instructions
refactor(powershell): consolidate execution logic
```

#### Pull Request Guidelines
- Keep PRs focused on a single feature/fix
- Update documentation if needed
- Add tests for new functionality
- Ensure all existing tests pass
- Include screenshots for UI changes

## 🧪 Testing

### Manual Testing
- Test on different Windows versions (10, 11)
- Test with various DHCP server configurations
- Verify admin rights scenarios
- Test network connectivity edge cases

### Automated Testing
```bash
# Run unit tests (when available)
dotnet test

# Build all configurations
dotnet build -c Debug
dotnet build -c Release
```

## 📋 Code Review Process

1. **Automated Checks**: All PRs must pass automated builds
2. **Code Review**: At least one maintainer review required
3. **Testing**: Manual testing for UI/functionality changes
4. **Documentation**: Updates to README/docs if needed

## 🎯 Areas for Contribution

### High Priority
- Unit tests for core functionality
- Internationalization (i18n) support
- Performance optimizations
- Additional DHCP server compatibility

### Medium Priority
- UI/UX improvements
- Additional export formats
- Enhanced logging and diagnostics
- Configuration management improvements

### Low Priority
- Code refactoring and cleanup
- Documentation improvements
- Example configurations
- Additional utility functions

## 🛡️ Security

If you discover a security vulnerability, please:
1. **DO NOT** create a public issue
2. Email the maintainer directly
3. Provide detailed information about the vulnerability
4. Allow time for the issue to be addressed before disclosure

## 📞 Getting Help

- **Questions**: Use [GitHub Discussions](https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer/discussions)
- **Issues**: Use [GitHub Issues](https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer/issues)
- **Chat**: Join our community discussions

## 📄 License

By contributing, you agree that your contributions will be licensed under the same license as the project (MIT License).

## 🙏 Recognition

Contributors will be recognized in:
- README.md acknowledgments
- Release notes
- GitHub contributors page

Thank you for helping make DHCP WMI Viewer better! 🚀
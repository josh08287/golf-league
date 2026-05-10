import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_providers.dart';
import '../auth/auth_service.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _mfaCodeController = TextEditingController();

  bool _loading = false;
  String? _error;

  // Set when the server returns an MFA-required response; the user then enters
  // a 6-digit code to complete login.
  String? _mfaToken;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _mfaCodeController.dispose();
    super.dispose();
  }

  Future<void> _onAuthSuccess() async {
    await ref.read(myStatusProvider.notifier).fetch();
    if (!mounted) return;
    final status = ref.read(myStatusProvider).status;
    if (status == 'approved') {
      context.go('/');
    } else {
      context.go('/not-invited');
    }
  }

  Future<void> _runAuth(Future<AuthResult> Function() action) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await action();
      if (result.mfaRequired) {
        setState(() {
          _loading = false;
          _mfaToken = result.accessToken;
        });
        return;
      }
      ref.read(authResultProvider.notifier).state = result;
      if (mounted) await _onAuthSuccess();
    } on AuthException catch (e) {
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (e) {
      setState(() {
        _loading = false;
        _error = _errorMessage(e);
      });
    }
  }

  Future<void> _passwordLogin() {
    return _runAuth(() => ref
        .read(authServiceProvider)
        .loginWithPassword(_emailController.text.trim(), _passwordController.text));
  }

  Future<void> _socialLogin(String provider) {
    return _runAuth(() => ref.read(authServiceProvider).loginWithSocial(provider));
  }

  Future<void> _verifyMfa() {
    final token = _mfaToken;
    if (token == null) return Future.value();
    return _runAuth(() => ref
        .read(authServiceProvider)
        .verifyTotp(mfaToken: token, code: _mfaCodeController.text.trim()));
  }

  @override
  Widget build(BuildContext context) {
    final isMfa = _mfaToken != null;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 380),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('⛳', style: TextStyle(fontSize: 64)),
                  const SizedBox(height: 16),
                  Text(
                    isMfa ? 'Two-step verification' : 'Sign in',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                  ),
                  const SizedBox(height: 24),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text(
                        _error!,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  if (isMfa) _buildMfaForm() else _buildLoginForm(),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLoginForm() {
    return Column(
      children: [
        TextField(
          controller: _emailController,
          decoration: const InputDecoration(
            labelText: 'Email',
            border: OutlineInputBorder(),
          ),
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.email],
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _passwordController,
          decoration: const InputDecoration(
            labelText: 'Password',
            border: OutlineInputBorder(),
          ),
          obscureText: true,
          autofillHints: const [AutofillHints.password],
        ),
        const SizedBox(height: 16),
        SizedBox(
          width: double.infinity,
          height: 48,
          child: FilledButton(
            onPressed: _loading ? null : _passwordLogin,
            child: _loading
                ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Sign in'),
          ),
        ),
        const SizedBox(height: 24),
        const Row(children: [
          Expanded(child: Divider()),
          Padding(padding: EdgeInsets.symmetric(horizontal: 12), child: Text('or')),
          Expanded(child: Divider()),
        ]),
        const SizedBox(height: 16),
        SizedBox(
          width: double.infinity,
          height: 48,
          child: OutlinedButton.icon(
            onPressed: _loading ? null : () => _socialLogin('google'),
            icon: const Icon(Icons.g_mobiledata),
            label: const Text('Continue with Google'),
          ),
        ),
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          height: 48,
          child: OutlinedButton.icon(
            onPressed: _loading ? null : () => _socialLogin('facebook'),
            icon: const Icon(Icons.facebook),
            label: const Text('Continue with Facebook'),
          ),
        ),
      ],
    );
  }

  Widget _buildMfaForm() {
    return Column(
      children: [
        const Text(
          'Enter the 6-digit code from your authenticator app.',
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _mfaCodeController,
          decoration: const InputDecoration(
            labelText: 'Code',
            border: OutlineInputBorder(),
          ),
          keyboardType: TextInputType.number,
          maxLength: 8,
          autofillHints: const [AutofillHints.oneTimeCode],
        ),
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          height: 48,
          child: FilledButton(
            onPressed: _loading ? null : _verifyMfa,
            child: _loading
                ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Verify'),
          ),
        ),
        TextButton(
          onPressed: _loading
              ? null
              : () => setState(() {
                    _mfaToken = null;
                    _mfaCodeController.clear();
                  }),
          child: const Text('Back'),
        ),
      ],
    );
  }

  String _errorMessage(Object e) {
    final dynamic dynamicError = e;
    try {
      final response = dynamicError.response;
      final data = response?.data;
      if (data is Map && data['error'] is String) return data['error'] as String;
    } catch (_) {}
    return 'Sign-in failed. Please try again.';
  }
}

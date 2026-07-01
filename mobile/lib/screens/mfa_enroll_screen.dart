import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_providers.dart';

/// TOTP enrollment: fetches a shared secret, lets the user add it to an
/// authenticator app, then verifies the first code. When [challengeToken] is
/// set (first-login enrollment) it is used instead of the stored access token.
class MfaEnrollScreen extends ConsumerStatefulWidget {
  const MfaEnrollScreen({super.key, this.challengeToken});

  final String? challengeToken;

  @override
  ConsumerState<MfaEnrollScreen> createState() => _MfaEnrollScreenState();
}

class _MfaEnrollScreenState extends ConsumerState<MfaEnrollScreen> {
  final _codeController = TextEditingController();
  String? _secret;
  String? _otpAuthUri;
  bool _loading = true;
  bool _verifying = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _startEnrollment();
  }

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _startEnrollment() async {
    try {
      final result = await ref
          .read(authServiceProvider)
          .startTotpEnrollment(bearerOverride: widget.challengeToken);
      if (!mounted) return;
      setState(() {
        _secret = result.secret;
        _otpAuthUri = result.otpAuthUri;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not start enrollment. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _verify() async {
    final code = _codeController.text.trim();
    if (code.length < 6) {
      setState(() => _error = 'Enter the 6-digit code from your app.');
      return;
    }
    setState(() {
      _verifying = true;
      _error = null;
    });
    try {
      await ref
          .read(authServiceProvider)
          .verifyTotpEnrollment(code, bearerOverride: widget.challengeToken);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Two-step verification enabled.')),
      );
      // After first-login enrollment the user must sign in again to get a
      // full token; otherwise just leave the screen.
      if (widget.challengeToken != null) {
        context.go('/login');
      } else {
        context.pop();
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Invalid code. Please try again.');
      }
    } finally {
      if (mounted) setState(() => _verifying = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(title: const Text('Set Up Two-Step Verification')),
      body: SafeArea(
        child: _loading
            ? const Center(child: CircularProgressIndicator())
            : SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: Center(
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 420),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        if (_error != null)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: Text(
                              _error!,
                              style: TextStyle(
                                color: Theme.of(context).colorScheme.error,
                              ),
                              textAlign: TextAlign.center,
                            ),
                          ),
                        if (_secret != null) ...[
                          const Text(
                            '1. Add this account to your authenticator app',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 8),
                          const Text(
                            'Enter the secret key below in Google Authenticator, '
                            '1Password, or any TOTP app.',
                            style: TextStyle(color: Color(0xFF6B7280)),
                          ),
                          const SizedBox(height: 12),
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(8),
                              border:
                                  Border.all(color: const Color(0xFFE5E7EB)),
                            ),
                            child: Row(
                              children: [
                                Expanded(
                                  child: SelectableText(
                                    _secret!,
                                    style: const TextStyle(
                                      fontFamily: 'monospace',
                                      fontSize: 15,
                                      letterSpacing: 1.5,
                                    ),
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.copy, size: 18),
                                  tooltip: 'Copy secret',
                                  onPressed: () async {
                                    await Clipboard.setData(
                                      ClipboardData(text: _secret!),
                                    );
                                    if (context.mounted) {
                                      ScaffoldMessenger.of(context)
                                          .showSnackBar(
                                        const SnackBar(
                                          content: Text('Secret copied'),
                                        ),
                                      );
                                    }
                                  },
                                ),
                              ],
                            ),
                          ),
                          if (_otpAuthUri != null &&
                              _otpAuthUri!.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            TextButton.icon(
                              icon: const Icon(Icons.link, size: 18),
                              label: const Text('Copy setup link (otpauth://)'),
                              onPressed: () async {
                                await Clipboard.setData(
                                  ClipboardData(text: _otpAuthUri!),
                                );
                                if (context.mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(
                                      content: Text('Setup link copied'),
                                    ),
                                  );
                                }
                              },
                            ),
                          ],
                          const SizedBox(height: 24),
                          const Text(
                            '2. Enter the 6-digit code',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          TextField(
                            controller: _codeController,
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
                            height: 48,
                            child: FilledButton(
                              onPressed: _verifying ? null : _verify,
                              child: _verifying
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Text('Verify & enable'),
                            ),
                          ),
                        ] else
                          FilledButton(
                            onPressed: () {
                              setState(() {
                                _loading = true;
                                _error = null;
                              });
                              _startEnrollment();
                            },
                            child: const Text('Retry'),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
      ),
    );
  }
}

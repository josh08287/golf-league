import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_providers.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _firstName;
  late final TextEditingController _lastName;
  late final TextEditingController _email;
  late final TextEditingController _phone;

  @override
  void initState() {
    super.initState();
    final auth = ref.read(authResultProvider);
    _firstName = TextEditingController(text: auth?.givenName ?? '');
    _lastName = TextEditingController(text: auth?.familyName ?? '');
    _email = TextEditingController(text: auth?.email ?? '');
    _phone = TextEditingController(text: auth?.phone ?? '');
  }

  @override
  void dispose() {
    _firstName.dispose();
    _lastName.dispose();
    _email.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    await ref.read(myStatusProvider.notifier).submitRegistration(
          firstName: _firstName.text.trim(),
          lastName: _lastName.text.trim(),
          email: _email.text.trim(),
          phone: _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        );
  }

  @override
  Widget build(BuildContext context) {
    final statusState = ref.watch(myStatusProvider);

    // Approved — go home
    if (statusState.status == 'approved') {
      WidgetsBinding.instance.addPostFrameCallback((_) => context.go('/'));
    }

    // Already submitted — show pending screen
    if (statusState.status == 'pending') {
      return const _PendingScreen();
    }

    final isRejected = statusState.status == 'rejected';

    return Scaffold(
      appBar: AppBar(title: const Text('Request to Join')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (isRejected) ...[
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.red.shade50,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: Colors.red.shade200),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Previous request declined.',
                          style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Colors.red)),
                      if (statusState.rejectionReason != null)
                        Text(
                          'Reason: ${statusState.rejectionReason}',
                          style: const TextStyle(color: Colors.red),
                        ),
                      const Text(
                          'You can update your details and re-submit below.',
                          style: TextStyle(color: Colors.red)),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
              ],
              _field('First Name', _firstName, required: true),
              const SizedBox(height: 16),
              _field('Last Name', _lastName, required: true),
              const SizedBox(height: 16),
              _field('Email', _email,
                  required: true,
                  keyboardType: TextInputType.emailAddress,
                  validator: (v) {
                    if (v == null || v.isEmpty) return 'Email is required';
                    if (!v.contains('@')) return 'Enter a valid email';
                    return null;
                  }),
              const SizedBox(height: 16),
              _field('Phone (optional)', _phone,
                  keyboardType: TextInputType.phone, required: false),
              const SizedBox(height: 8),
              if (statusState.error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Text(
                    statusState.error!,
                    style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                        fontSize: 13),
                  ),
                ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: FilledButton(
                  onPressed: statusState.isLoading ? null : _submit,
                  child: statusState.isLoading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child:
                              CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                      : const Text('Request to Join'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _field(
    String label,
    TextEditingController controller, {
    bool required = true,
    TextInputType? keyboardType,
    String? Function(String?)? validator,
  }) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: label,
        border: const OutlineInputBorder(),
      ),
      validator: validator ??
          (required
              ? (v) => (v == null || v.trim().isEmpty) ? '$label is required' : null
              : null),
    );
  }
}

class _PendingScreen extends StatelessWidget {
  const _PendingScreen();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Request Submitted')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Text('⏳', style: TextStyle(fontSize: 64)),
              const SizedBox(height: 16),
              Text(
                'Pending Approval',
                style: Theme.of(context)
                    .textTheme
                    .headlineSmall
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              Text(
                'Your request to join the league has been submitted '
                'and is pending admin review. Check back soon.',
                textAlign: TextAlign.center,
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Colors.grey[600]),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Plus, ChevronDown, ChevronUp } from 'lucide-react';
import { api } from '../../lib/api';
import { useCreateCourse, courseKeys } from '../../hooks/admin/useCourseMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { FormField, inputClass } from '../../components/admin/FormField';
import { CourseHolesEditor } from '../../components/admin/CourseHolesEditor';
import type { Course, CourseDetail } from '../../types/api';

// ── Schema ─────────────────────────────────────────────────────────────────

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  rating: z.number({ invalid_type_error: 'Required' }).min(60).max(80),
  slope: z.number({ invalid_type_error: 'Required' }).int().min(55).max(155),
});

type FormValues = z.infer<typeof schema>;

// ── Add Course Form ────────────────────────────────────────────────────────

interface AddCourseFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

function AddCourseForm({ onSuccess, onCancel }: AddCourseFormProps) {
  const createCourse = useCreateCourse();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    await createCourse.mutateAsync(values);
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Course Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} placeholder="Pine Valley GC" />
      </FormField>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Course Rating" error={errors.rating} required hint="e.g. 71.3">
          <input
            {...register('rating', { valueAsNumber: true })}
            type="number"
            step="0.1"
            className={inputClass}
          />
        </FormField>
        <FormField label="Slope Rating" error={errors.slope} required hint="55–155">
          <input
            {...register('slope', { valueAsNumber: true })}
            type="number"
            className={inputClass}
          />
        </FormField>
      </div>

      {createCourse.isError && (
        <p className="text-sm text-red-600">Failed to create course. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || createCourse.isPending}>
          Add Course
        </Button>
      </div>
    </form>
  );
}

// ── Course Row ─────────────────────────────────────────────────────────────

interface CourseRowProps {
  course: Course;
}

function CourseRow({ course }: CourseRowProps) {
  const [expanded, setExpanded] = useState(false);

  const { data: detail, isLoading } = useQuery<CourseDetail>({
    queryKey: courseKeys.detail(String(course.id)),
    queryFn: () => api.get(`/courses/${course.id}`).then((r) => r.data),
    enabled: expanded,
  });

  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
      {/* Header row */}
      <div
        className="flex cursor-pointer items-center justify-between px-5 py-4 hover:bg-gray-50"
        onClick={() => setExpanded((v) => !v)}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') setExpanded((v) => !v);
        }}
      >
        <div>
          <h3 className="font-semibold text-gray-900">{course.name}</h3>
          <p className="text-xs text-gray-500">
            Rating: {course.rating} &nbsp;|&nbsp; Slope: {course.slope}
          </p>
        </div>
        <div className="text-gray-400">
          {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
        </div>
      </div>

      {/* Holes editor (expanded) */}
      {expanded && (
        <div className="border-t border-gray-100 px-5 py-4">
          {isLoading ? (
            <Spinner />
          ) : (
            <CourseHolesEditor
              courseId={String(course.id)}
              initialHoles={detail?.holeDetails ?? []}
            />
          )}
        </div>
      )}
    </div>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────

export function CoursesPage() {
  const [addOpen, setAddOpen] = useState(false);

  const { data: coursesPage, isLoading, error } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <ErrorMessage message="Failed to load courses." />;
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Courses">
        <Button variant="primary" onClick={() => setAddOpen(true)}>
          <Plus className="mr-1 h-4 w-4" />
          Add Course
        </Button>
      </PageHeader>

      <div className="space-y-3">
        {courses.map((c) => (
          <CourseRow key={c.id} course={c} />
        ))}
        {courses.length === 0 && (
          <p className="text-sm text-gray-500">No courses yet.</p>
        )}
      </div>

      {/* Add Course Modal */}
      <Modal open={addOpen} title="Add Course" onClose={() => setAddOpen(false)}>
        <AddCourseForm
          onSuccess={() => setAddOpen(false)}
          onCancel={() => setAddOpen(false)}
        />
      </Modal>
    </div>
  );
}
